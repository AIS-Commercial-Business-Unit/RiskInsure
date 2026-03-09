namespace RiskInsure.RiskRatingAndUnderwriting.Infrastructure;

using Microsoft.Azure.Cosmos;
using System.Text.Json;

public class CosmosSystemTextJsonSerializer : CosmosSerializer
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    private readonly JsonSerializerOptions _options;

    public CosmosSystemTextJsonSerializer()
        : this(DefaultOptions)
    {
    }

    public CosmosSystemTextJsonSerializer(JsonSerializerOptions options)
    {
        _options = options;
    }

    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (stream.CanSeek && stream.Length == 0)
            {
                return default!;
            }

            return JsonSerializer.Deserialize<T>(stream, _options)!;
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, input, _options);
        stream.Position = 0;
        return stream;
    }
}
