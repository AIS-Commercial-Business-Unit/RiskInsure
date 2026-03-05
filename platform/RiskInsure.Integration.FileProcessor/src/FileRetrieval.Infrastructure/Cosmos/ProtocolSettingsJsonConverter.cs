using System.Text.Json;
using System.Text.Json.Serialization;
using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.ValueObjects;

namespace RiskInsure.FileRetrieval.Infrastructure.Cosmos;

public sealed class ProtocolSettingsJsonConverter : JsonConverter<ProtocolSettings>
{
    public override ProtocolSettings Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var raw = root.GetRawText();

        var protocolType = ResolveProtocolType(root);

        return protocolType switch
        {
            ProtocolType.FTP => JsonSerializer.Deserialize<FtpProtocolSettings>(raw, options)
                ?? throw new JsonException("Unable to deserialize FTP protocol settings."),
            ProtocolType.HTTPS => JsonSerializer.Deserialize<HttpsProtocolSettings>(raw, options)
                ?? throw new JsonException("Unable to deserialize HTTPS protocol settings."),
            ProtocolType.AzureBlob => JsonSerializer.Deserialize<AzureBlobProtocolSettings>(raw, options)
                ?? throw new JsonException("Unable to deserialize AzureBlob protocol settings."),
            _ => throw new JsonException($"Unsupported protocol type '{protocolType}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, ProtocolSettings value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
    }

    private static ProtocolType ResolveProtocolType(JsonElement root)
    {
        if (TryGetProtocolTypeFromDiscriminator(root, "$protocolType", out var protocolType))
        {
            return protocolType;
        }

        if (TryGetProtocolTypeFromDiscriminator(root, "protocolType", out protocolType))
        {
            return protocolType;
        }

        if (TryGetProtocolTypeFromDiscriminator(root, "ProtocolType", out protocolType))
        {
            return protocolType;
        }

        if (root.TryGetProperty("server", out _) || root.TryGetProperty("Server", out _))
        {
            return ProtocolType.FTP;
        }

        if (root.TryGetProperty("baseUrl", out _) || root.TryGetProperty("BaseUrl", out _))
        {
            return ProtocolType.HTTPS;
        }

        if (root.TryGetProperty("storageAccountName", out _) || root.TryGetProperty("StorageAccountName", out _))
        {
            return ProtocolType.AzureBlob;
        }

        throw new JsonException("Protocol settings payload did not include a recognizable protocol type discriminator.");
    }

    private static bool TryGetProtocolTypeFromDiscriminator(JsonElement root, string propertyName, out ProtocolType protocolType)
    {
        protocolType = default;

        if (!root.TryGetProperty(propertyName, out var discriminator))
        {
            return false;
        }

        if (discriminator.ValueKind == JsonValueKind.String)
        {
            var value = discriminator.GetString();

            if (Enum.TryParse<ProtocolType>(value, ignoreCase: true, out protocolType))
            {
                return true;
            }

            return false;
        }

        if (discriminator.ValueKind == JsonValueKind.Number && discriminator.TryGetInt32(out var numeric))
        {
            protocolType = (ProtocolType)numeric;
            return Enum.IsDefined(protocolType);
        }

        return false;
    }
}
