using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

public static class JsonPathModifierExtensions
{
    // Attach this to DefaultJsonTypeInfoResolver.Modifiers
    public static void JsonPathModifier(JsonTypeInfo ti)
    {
        // In current System.Text.Json, JsonTypeInfo does not expose DeserializeHandler/SerializeHandler.
        // Keep this hook as a no-op for compatibility with existing modifier registration.
        _ = ti;
    }

    public static void AddJsonPathConverters(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Converters.OfType<JsonPathConverterFactory>().Any())
        {
            options.Converters.Add(new JsonPathConverterFactory());
        }
    }

    // ---- internal mapping structs ----
    private sealed class PropMap
    {
        public required PropertyInfo Prop { get; init; }
        public required Type PropType { get; init; }
        public string? Path { get; init; }                 // [JsonPath] => JSON Pointer path or "$"
        public string? JsonNameOverride { get; init; }     // [JsonPropertyName] if present
        public MethodInfo? Getter { get; init; }
        public MethodInfo? Setter { get; init; }
    }

    private sealed class Maps
    {
        public required Type Type { get; init; }
        public required PropMap[] Properties { get; init; }
    }

    private static Maps BuildMaps(Type t)
    {
        var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.GetIndexParameters().Length == 0)
                     .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
                     .Select(p => new PropMap
                     {
                         Prop = p,
                         PropType = p.PropertyType,
                         Path = p.GetCustomAttribute<JsonPathAttribute>()?.Path,
                         JsonNameOverride = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name,
                         Getter = p.GetGetMethod(nonPublic: false),
                         Setter = p.GetSetMethod(nonPublic: false)
                     })
                     .ToArray();

        return new Maps { Type = t, Properties = props };
    }

    // ---- READ ----
    private static object? ReadObject(Type t, Maps maps, ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var obj = CreateInstance(t);

        foreach (var m in maps.Properties)
        {
            JsonElement source;

            if (!string.IsNullOrEmpty(m.Path))
            {
                if (!TrySelect(root, m.Path!, out source))
                    continue; // not found, leave default
            }
            else
            {
                if (root.ValueKind != JsonValueKind.Object)
                    continue;

                if (!TryGetByName(root, EffectiveReadNames(m, options), out source))
                    continue;
            }

            object? value = source.ValueKind == JsonValueKind.Null
                ? null
                : source.Deserialize(m.PropType, options);

            m.Setter?.Invoke(obj, new[] { value });
        }

        return obj;
    }

    // ---- WRITE ----
    private static void WriteObject(Type t, Maps maps, Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        // Root-wins if a property targets "$" and has non-null value
        var rootMap = maps.Properties.FirstOrDefault(p => p.Path is "$" or "");
        if (rootMap is not null)
        {
            var rootVal = rootMap.Getter?.Invoke(value, null);
            if (rootVal is not null)
            {
                if (rootMap.PropType == t)
                    throw new NotSupportedException($"[JsonPath(\"$\")] cannot target the same type {t.Name} (infinite recursion).");

                JsonSerializer.Serialize(writer, rootVal, rootMap.PropType, options);
                return;
            }
        }

        var rootNode = new JsonObject();

        // Write non-path properties at root
        foreach (var m in maps.Properties.Where(p => string.IsNullOrEmpty(p.Path)))
        {
            var v = m.Getter?.Invoke(value, null);
            var node = SerializeToNode(v, m.PropType, options);
            var name = EffectiveWriteName(m, options);
            if (name is null) continue;
            rootNode[name] = node;
        }

        // Write [JsonPath]-bound properties at their pointers (except "$")
        foreach (var m in maps.Properties.Where(p => !string.IsNullOrEmpty(p.Path) && p.Path != "$"))
        {
            var v = m.Getter?.Invoke(value, null);
            var node = SerializeToNode(v, m.PropType, options);
            EnsurePathAndSet(rootNode, m.Path!, node);
        }

        rootNode.WriteTo(writer, options);
    }

    // ---- helpers ----
    private static object CreateInstance(Type t)
    {
        if (t.IsValueType) return Activator.CreateInstance(t)!;
        var ctor = t.GetConstructor(Type.EmptyTypes)
            ?? throw new NotSupportedException($"{t} requires a parameterless constructor.");
        return ctor.Invoke(null);
    }

    private static bool TryGetByName(JsonElement obj, IEnumerable<string> names, out JsonElement found)
    {
        foreach (var name in names)
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

        var policyName = options.PropertyNamingPolicy?.ConvertName(m.Prop.Name);
        if (!string.IsNullOrEmpty(policyName)) yield return policyName!;
        yield return m.Prop.Name; // CLR name as fallback
    }

    private static string? EffectiveWriteName(PropMap m, JsonSerializerOptions options)
    {
        if (!string.IsNullOrEmpty(m.JsonNameOverride))
            return m.JsonNameOverride;

        return options.PropertyNamingPolicy?.ConvertName(m.Prop.Name) ?? m.Prop.Name;
    }

    private static JsonNode? SerializeToNode(object? value, Type type, JsonSerializerOptions options)
        => value is null ? null : JsonSerializer.SerializeToNode(value, type, options);

    /// Minimal JSON Pointer evaluator with "$" support
    private static bool TrySelect(JsonElement root, string path, out JsonElement selected)
    {
        selected = root;
        if (path == "$" || string.IsNullOrEmpty(path))
            return true;

        if (path.StartsWith("$/", StringComparison.Ordinal))
            path = path[1..];

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

    /// Create intermediate nodes and set leaf at JSON Pointer path.
    private static void EnsurePathAndSet(JsonObject root, string path, JsonNode? leaf)
    {
        if (path == "$" || string.IsNullOrEmpty(path))
            throw new NotSupportedException("Root ('$') is handled separately.");

        if (path.StartsWith("$/", StringComparison.Ordinal))
            path = path[1..];

        if (!path.StartsWith("/", StringComparison.Ordinal))
            path = "/" + path;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                           .Select(Unescape)
                           .ToArray();

        JsonNode current = root;

        for (int i = 0; i < segments.Length; i++)
        {
            string seg = segments[i];
            bool last = i == segments.Length - 1;

            if (current is JsonObject jobj)
            {
                if (last)
                {
                    jobj[seg] = leaf;
                    return;
                }

                if (!jobj.TryGetPropertyValue(seg, out var next) || next is null)
                {
                    var nextSeg = segments[i + 1];
                    next = int.TryParse(nextSeg, out _) ? new JsonArray() : new JsonObject();
                    jobj[seg] = next;
                }
                current = jobj[seg]!;
            }
            else if (current is JsonArray jarr)
            {
                if (!int.TryParse(seg, out int idx))
                    throw new JsonException($"Segment '{seg}' must be an array index.");

                while (jarr.Count <= idx) jarr.Add(null);

                if (last)
                {
                    jarr[idx] = leaf;
                    return;
                }

                if (jarr[idx] is null)
                {
                    var nextSeg = segments[i + 1];
                    jarr[idx] = int.TryParse(nextSeg, out _) ? new JsonArray() : new JsonObject();
                }
                current = jarr[idx]!;
            }
            else
            {
                var replacement = new JsonObject();
                current.ReplaceWith(replacement);
                current = replacement;
                i--; // retry this segment
            }
        }

        static string Unescape(string t) => t.Replace("~1", "/").Replace("~0", "~");
    }
}