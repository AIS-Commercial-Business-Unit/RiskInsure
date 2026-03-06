using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class JsonPathConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return CanConvertInternal(typeToConvert, depth: 0);
    }

    private bool CanConvertInternal(Type typeToConvert, int depth)
    {
        if (depth >= 5)
            return false;

        var properties = typeToConvert
            .GetProperties(BindingFlags.Instance | BindingFlags.Public);

        if (properties.Any(p => p.GetCustomAttribute<JsonPathAttribute>(inherit: true) is not null))
            return true;

        if (properties.Any(p => CanConvertInternal(p.PropertyType, depth + 1)))
            return true;

        // For abstract classes and interfaces, also scan concrete derived types in the same
        // assembly. This ensures that a parent like FileRetrievalConfiguration (which has a
        // declared ProtocolSettings property) is handled by the converter even though
        // [JsonPath] only appears on concrete subtypes like FtpProtocolSettings.Password.
        if (typeToConvert.IsAbstract || typeToConvert.IsInterface)
        {
            foreach (var derived in typeToConvert.Assembly.GetTypes()
                         .Where(t => !t.IsAbstract && t != typeToConvert && typeToConvert.IsAssignableFrom(t)))
            {
                if (CanConvertInternal(derived, depth + 1))
                    return true;
            }
        }

        return false;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(JsonPathObjectConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}
