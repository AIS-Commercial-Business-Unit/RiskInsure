namespace RiskInsure.Billing.Infrastructure;

using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Cosmos;

/// <summary>
/// Custom Cosmos DB serializer using System.Text.Json instead of Newtonsoft.Json.
/// Required to properly serialize documents with [JsonPropertyName] attributes.
/// </summary>
public class CosmosSystemTextJsonSerializer : CosmosSerializer
{
    private readonly JsonSerializerOptions _options;

    public CosmosSystemTextJsonSerializer(JsonSerializerOptions options)
    {
        _options = options;
    }

    public override T FromStream<T>(Stream stream)
    {
        if (stream == null || stream.Length == 0)
        {
            return default!;
        }

        using (stream)
        {
            return JsonSerializer.Deserialize<T>(stream, _options)!;
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var json = JsonSerializer.Serialize(input, _options);
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }
}
