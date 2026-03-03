using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

/// <summary>
/// In System.Text.Json, a property converter only sees the JSON token 
/// for that property’s value—it cannot inspect siblings or the root. 
/// To read from arbitrary locations, we must parse the whole object 
/// and set the property ourselves. The attribute is only a directive 
/// that tells the type-level converter where to look.
/// </summary>
public sealed class JsonPathObjectConverter<T> : JsonConverter<T>
{
    private sealed class PropMap
    {
        public PropertyInfo Prop { get; init; } = default!;
        public string? Path { get; init; }
        public string? JsonNameOverride { get; init; } // from [JsonPropertyName], if any
        public MethodInfo? Getter { get; init; }
        public MethodInfo? Setter { get; init; }
        public Type PropType { get; init; } = default!;
    }

    private static readonly PropMap[] _maps = BuildMaps();

    // ----------------------------
    // DESERIALIZATION
    // ----------------------------
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var obj = CreateInstance();

        foreach (var m in _maps)
        {
            JsonElement source;

            if (!string.IsNullOrEmpty(m.Path))
            {
                if (!TrySelect(root, m.Path!, out source)) continue; // not found: leave default
            }
            else
            {
                if (root.ValueKind != JsonValueKind.Object) continue;

                // Try attribute name first; else try naming policy; else CLR name.
                if (!TryGetByName(root, EffectiveReadNames(m, options), out source)) continue;
            }

            object? value = source.ValueKind == JsonValueKind.Null
                ? null
                : source.Deserialize(m.PropType, options);

            m.Setter!.Invoke(obj, new[] { value });
        }

        return obj;
    }

    // ----------------------------
    // SERIALIZATION
    // ----------------------------
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        // 1) If a property is mapped to "$" (root) and has a non-null value, it **becomes** the whole output.
        //    We serialize only that value (to avoid duplication/ambiguity).
        var rootMapped = _maps.FirstOrDefault(m => m.Path is "$" or "");
        if (rootMapped is not null)
        {
            var v = rootMapped.Getter!.Invoke(value, null);
            if (v is not null)
            {
                // Avoid infinite recursion if someone maps T to "$" on a property of type T (discouraged).
                if (rootMapped.PropType == typeof(T))
                    throw new NotSupportedException(
                        $"[JsonPath(\"$\")] cannot target the same type {typeof(T).Name} to avoid infinite recursion.");

                JsonSerializer.Serialize(writer, v, rootMapped.PropType, options);
                return;
            }
            // If it's null, just fall through and synthesize from other properties.
        }

        // 2) Build an output tree:
        //    - Start with an object root.
        //    - Write non-[JsonPath] properties by their normal names.
        //    - Then apply [JsonPath] properties to their paths, creating intermediate nodes.
        var rootNode = new JsonObject();

        // Non-path properties first (so [JsonPath] can override/augment)
        foreach (var m in _maps.Where(m => string.IsNullOrEmpty(m.Path)))
        {
            var v = m.Getter!.Invoke(value, null);
            var node = SerializeToNode(v, m.PropType, options);
            var name = EffectiveWriteName(m, options);
            if (name is null) continue; // should not happen
            rootNode[name] = node;
        }

        // Then path-bound properties (excluding "$")
        foreach (var m in _maps.Where(m => !string.IsNullOrEmpty(m.Path) && m.Path != "$"))
        {
            var v = m.Getter!.Invoke(value, null);
            var node = SerializeToNode(v, m.PropType, options);

            if (node is null)
            {
                // Explicitly set null at path (creates the path if needed, final leaf is null)
                EnsurePathAndSet(rootNode, m.Path!, null);
            }
            else
            {
                EnsurePathAndSet(rootNode, m.Path!, node);
            }
        }

        // Emit the synthesized tree
        rootNode.WriteTo(writer, options);
    }

    // ----------------------------
    // Helpers
    // ----------------------------

    private static T CreateInstance()
    {
        if (typeof(T).IsValueType) return default!;
        var ctor = typeof(T).GetConstructor(Type.EmptyTypes)
                  ?? throw new NotSupportedException($"{typeof(T)} requires a parameterless constructor.");
        return (T)ctor.Invoke(null);
    }

    private static PropMap[] BuildMaps()
    {
        var props = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null);

        var list = new List<PropMap>();
        foreach (var p in props)
        {
            var gm = p.GetGetMethod(nonPublic: false);
            var sm = p.GetSetMethod(nonPublic: false);
            if (gm is null && sm is null) continue; // neither get nor set

            list.Add(new PropMap
            {
                Prop = p,
                PropType = p.PropertyType,
                Path = p.GetCustomAttribute<JsonPathAttribute>()?.Path,
                JsonNameOverride = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name,
                Getter = gm,
                Setter = sm
            });
        }
        return list.ToArray();
    }

    private static bool TryGetByName(JsonElement obj, IEnumerable<string> candidateNames, out JsonElement found)
    {
        foreach (var name in candidateNames)
        {
            if (!string.IsNullOrEmpty(name) && obj.TryGetProperty(name!, out found))
                return true;
        }
        found = default;
        return false;
    }

    private static IEnumerable<string> EffectiveReadNames(PropMap m, JsonSerializerOptions options)
    {
        if (!string.IsNullOrEmpty(m.JsonNameOverride))
        {
            yield return m.JsonNameOverride!;
            yield break;
        }

        // Try policy name, then CLR name
        var policyName = options.PropertyNamingPolicy?.ConvertName(m.Prop.Name);
        if (!string.IsNullOrEmpty(policyName)) yield return policyName!;
        yield return m.Prop.Name;
    }

    private static string? EffectiveWriteName(PropMap m, JsonSerializerOptions options)
    {
        if (!string.IsNullOrEmpty(m.JsonNameOverride))
            return m.JsonNameOverride;

        return options.PropertyNamingPolicy?.ConvertName(m.Prop.Name) ?? m.Prop.Name;
    }

    private static JsonNode? SerializeToNode(object? value, Type type, JsonSerializerOptions options)
        => value is null ? null : JsonSerializer.SerializeToNode(value, type, options);

    /// <summary>
    /// Minimal JSON Pointer evaluator; also accepts "$" (root) and tolerates "a/b" vs "/a/b".
    /// </summary>
    private static bool TrySelect(JsonElement root, string path, out JsonElement selected)
    {
        selected = root;
        if (path == "$" || string.IsNullOrEmpty(path))
            return true;

        if (path.StartsWith("$/", StringComparison.Ordinal))
            path = path.Substring(1);

        if (!path.StartsWith("/", StringComparison.Ordinal))
            path = "/" + path;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                           .Select(Unescape)
                           .ToArray();

        var current = root;
        foreach (var seg in segments)
        {
            switch (current.ValueKind)
            {
                case JsonValueKind.Object:
                    if (!current.TryGetProperty(seg, out current))
                        return false;
                    break;

                case JsonValueKind.Array:
                    if (!int.TryParse(seg, out var idx)) return false;
                    if ((uint)idx >= (uint)current.GetArrayLength()) return false;
                    current = current[idx];
                    break;

                default:
                    return false;
            }
        }

        selected = current;
        return true;

        static string Unescape(string t) => t.Replace("~1", "/").Replace("~0", "~");
    }

    /// <summary>
    /// Ensures all intermediate containers exist and sets the leaf value at the JSON Pointer path.
    /// Creates objects/arrays as needed. Accepts "$", "/a/b/0", or "a/b".
    /// </summary>
    private static void EnsurePathAndSet(JsonObject root, string path, JsonNode? leaf)
    {
        if (path == "$" || string.IsNullOrEmpty(path))
            throw new NotSupportedException("Root ('$') path is handled separately in Write.");

        if (path.StartsWith("$/", StringComparison.Ordinal))
            path = path.Substring(1);

        if (!path.StartsWith("/", StringComparison.Ordinal))
            path = "/" + path;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                           .Select(Unescape)
                           .ToArray();

        JsonNode current = root;
        for (int i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            bool isLast = i == segments.Length - 1;

            if (current is JsonObject jobj)
            {
                if (isLast)
                {
                    jobj[seg] = leaf;
                    return;
                }

                if (!jobj.TryGetPropertyValue(seg, out var next) || next is null)
                {
                    // Heuristic: lookahead to decide object vs array
                    var nextSeg = segments[i + 1];
                    next = int.TryParse(nextSeg, out _) ? new JsonArray() : new JsonObject();
                    jobj[seg] = next;
                }
                current = jobj[seg]!;
            }
            else if (current is JsonArray jarr)
            {
                // Array index
                if (!int.TryParse(seg, out int idx))
                    throw new JsonException($"Path segment '{seg}' must be an array index when targeting an array.");

                // Grow as needed
                while (jarr.Count <= idx) jarr.Add(null);

                if (isLast)
                {
                    jarr[idx] = leaf;
                    return;
                }

                if (jarr[idx] is null)
                {
                    // Heuristic: lookahead decides object vs array
                    var nextSeg = segments[i + 1];
                    jarr[idx] = int.TryParse(nextSeg, out _) ? new JsonArray() : new JsonObject();
                }

                current = jarr[idx]!;
            }
            else
            {
                // If somehow we land on a primitive, replace with an object and continue.
                var replacement = new JsonObject();
                current.ReplaceWith(replacement);
                current = replacement;
                i--; // redo this segment with the new object
            }
        }

        static string Unescape(string t) => t.Replace("~1", "/").Replace("~0", "~");
    }
}