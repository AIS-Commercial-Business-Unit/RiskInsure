using RiskInsure.FileProcessing.Domain.Enums;
using System.Text.Json.Serialization;
using RiskInsure.FileProcessing.Domain.Serialization;

namespace RiskInsure.FileProcessing.Domain.ValueObjects;

/// <summary>
/// T024: FTP protocol-specific connection settings.
/// </summary>
public sealed class FtpProtocolSettings : ProtocolSettings
{
    [JsonIgnore]
    public override ProtocolType ProtocolType => ProtocolType.FTP;

    public required string Server { get; init; }
    public int Port { get; init; }
    public required string Username { get; init; }

    /// In order for this property to be encrypted, it has to reside
    /// at the root of the JSON being sent to Cosmos. The JsonPath 
    /// attribute causes this to be serialized/deserialized at a 
    /// specific location in the JSON document, using the provided path, 
    /// rather than as a nested property.
    [JsonPath(SecretPaths.FtpSecretPath)]
    public required string Password { get; init; }
    public bool UseTls { get; init; }
    public bool UsePassiveMode { get; init; }
    public TimeSpan ConnectionTimeout { get; init; }
}
