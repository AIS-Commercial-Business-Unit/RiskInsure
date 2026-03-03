using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

public sealed class JsonPathResolver : IJsonTypeInfoResolver
{
    private readonly IJsonTypeInfoResolver _inner;

    public JsonPathResolver(IJsonTypeInfoResolver? inner = null)
        => _inner = inner ?? new DefaultJsonTypeInfoResolver();

    public JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var ti = _inner.GetTypeInfo(type, options);
        if (ti.Kind != JsonTypeInfoKind.Object) return ti;

        var anyPath = ti.Properties.Any(p =>
            p.AttributeProvider?.IsDefined(typeof(JsonPathAttribute), inherit: true) == true);

        if (anyPath)
        {
            var converterType = typeof(JsonPathObjectConverter<>).MakeGenericType(type);
            ti.Converter = (System.Text.Json.Serialization.JsonConverter)Activator.CreateInstance(converterType)!;
        }
        return ti;
    }
}