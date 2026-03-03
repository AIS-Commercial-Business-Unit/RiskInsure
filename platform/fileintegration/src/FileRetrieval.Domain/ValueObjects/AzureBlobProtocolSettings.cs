using RiskInsure.FileRetrieval.Domain.Enums;

namespace RiskInsure.FileRetrieval.Domain.ValueObjects;

/// <summary>
/// T026: Azure Blob Storage protocol-specific connection settings.
/// </summary>
public sealed class AzureBlobProtocolSettings : ProtocolSettings
{
    public string StorageAccountName { get; init; }
    public string ContainerName { get; init; }
    public AzureAuthType AuthenticationType { get; init; }
    public string? ConnectionString { get; init; }
    public string? SasToken { get; init; }
    public string? BlobPrefix { get; init; }

    public AzureBlobProtocolSettings(
        string storageAccountName,
        string containerName,
        AzureAuthType authenticationType,
        string? connectionString = null,
        string? SasToken = null,
        string? blobPrefix = null)
        : base(ProtocolType.AzureBlob)
    {
        if (string.IsNullOrWhiteSpace(storageAccountName))
            throw new ArgumentException("StorageAccountName cannot be empty", nameof(storageAccountName));
        if (storageAccountName.Length > 24)
            throw new ArgumentException("StorageAccountName cannot exceed 24 characters", nameof(storageAccountName));
        if (string.IsNullOrWhiteSpace(containerName))
            throw new ArgumentException("ContainerName cannot be empty", nameof(containerName));
        if (containerName.Length > 63)
            throw new ArgumentException("ContainerName cannot exceed 63 characters", nameof(containerName));
        if (containerName != containerName.ToLowerInvariant())
            throw new ArgumentException("ContainerName must be lowercase", nameof(containerName));
        if (blobPrefix?.Length > 1024)
            throw new ArgumentException("BlobPrefix cannot exceed 1024 characters", nameof(blobPrefix));

        // Validate auth-specific requirements
        if (authenticationType == AzureAuthType.ConnectionString && string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("ConnectionString is required for ConnectionString authentication", nameof(connectionString));
        if (authenticationType == AzureAuthType.SasToken && string.IsNullOrWhiteSpace(SasToken))
            throw new ArgumentException("SasToken is required for SasToken authentication", nameof(SasToken));

        StorageAccountName = storageAccountName;
        ContainerName = containerName;
        AuthenticationType = authenticationType;
        ConnectionString = connectionString;
        SasToken = SasToken;
        BlobPrefix = blobPrefix;
    }
}
