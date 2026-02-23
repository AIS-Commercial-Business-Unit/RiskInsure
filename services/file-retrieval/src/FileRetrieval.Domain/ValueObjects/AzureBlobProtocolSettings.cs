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
    public string? ConnectionStringKeyVaultSecret { get; init; }
    public string? SasTokenKeyVaultSecret { get; init; }
    public string? BlobPrefix { get; init; }

    public AzureBlobProtocolSettings(
        string storageAccountName,
        string containerName,
        AzureAuthType authenticationType,
        string? connectionStringKeyVaultSecret = null,
        string? sasTokenKeyVaultSecret = null,
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
        if (authenticationType == AzureAuthType.ConnectionString && string.IsNullOrWhiteSpace(connectionStringKeyVaultSecret))
            throw new ArgumentException("ConnectionStringKeyVaultSecret is required for ConnectionString authentication", nameof(connectionStringKeyVaultSecret));
        if (authenticationType == AzureAuthType.SasToken && string.IsNullOrWhiteSpace(sasTokenKeyVaultSecret))
            throw new ArgumentException("SasTokenKeyVaultSecret is required for SasToken authentication", nameof(sasTokenKeyVaultSecret));

        StorageAccountName = storageAccountName;
        ContainerName = containerName;
        AuthenticationType = authenticationType;
        ConnectionStringKeyVaultSecret = connectionStringKeyVaultSecret;
        SasTokenKeyVaultSecret = sasTokenKeyVaultSecret;
        BlobPrefix = blobPrefix;
    }
}
