using RiskInsure.FileProcessing.Domain.Enums;
using RiskInsure.FileProcessing.Domain.Serialization;
using System.Text.Json.Serialization;

namespace RiskInsure.FileProcessing.Domain.ValueObjects;

/// <summary>
/// T026: Azure Blob Storage protocol-specific connection settings.
/// </summary>
public sealed class AzureBlobProtocolSettings : ProtocolSettings
{
    [JsonIgnore]
    public override ProtocolType ProtocolType => ProtocolType.AzureBlob;

    public required string StorageAccountName { get; init; }
    public required string ContainerName { get; init; }
    public AzureAuthType AuthenticationType { get; init; }

    /// <summary>
    /// In order for this property to be encrypted, it has to reside
    /// at the root of the JSON being sent to Cosmos. The JsonPath 
    /// attribute causes this to be serialized/deserialized at a 
    /// specific location in the JSON document, using the provided path, 
    /// rather than as a nested property.
    /// </summary>
    [JsonPath(SecretPaths.AzureBlobSecretPath)]
    public string? ConnectionString { get; init; }
    public string? SasToken { get; init; }
    public string? BlobPrefix { get; init; }
}
