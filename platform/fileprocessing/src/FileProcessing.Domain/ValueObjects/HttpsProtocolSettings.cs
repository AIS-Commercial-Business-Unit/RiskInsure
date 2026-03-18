using RiskInsure.FileProcessing.Domain.Enums;
using System.Text.Json.Serialization;
using RiskInsure.FileProcessing.Domain.Serialization;

namespace RiskInsure.FileProcessing.Domain.ValueObjects;

/// <summary>
/// T025: HTTPS protocol-specific connection settings.
/// </summary>
public sealed class HttpsProtocolSettings : ProtocolSettings
{
    [JsonIgnore]
    public override ProtocolType ProtocolType => ProtocolType.HTTPS;

    public required string BaseUrl { get; init; }
    public AuthType AuthenticationType { get; init; }
    public string? Username { get; init; }

    /// In order for this property to be encrypted, it has to reside
    /// at the root of the JSON being sent to Cosmos. The JsonPath 
    /// attribute causes this to be serialized/deserialized at a 
    /// specific location in the JSON document, using the provided path, 
    /// rather than as a nested property.
    [JsonPath(SecretPaths.HttpsSecretPath)]
    public string? PasswordOrTokenOrApiKey { get; init; }

    public TimeSpan ConnectionTimeout { get; init; }
    public bool FollowRedirects { get; init; }
    public int MaxRedirects { get; init; }
}
