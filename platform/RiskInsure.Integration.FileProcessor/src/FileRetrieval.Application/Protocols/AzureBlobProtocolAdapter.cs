using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Identity;
using RiskInsure.FileRetrieval.Domain.ValueObjects;
using RiskInsure.FileRetrieval.Domain.Enums;
using Microsoft.Extensions.Logging;
using Azure.Security.KeyVault.Secrets;

namespace FileRetrieval.Application.Protocols;

/// <summary>
/// Protocol adapter for Azure Blob Storage file retrieval.
/// Supports three authentication methods: Managed Identity, Connection String, SAS Token.
/// Uses Azure.Storage.Blobs SDK for blob operations.
/// </summary>
public class AzureBlobProtocolAdapter : IProtocolAdapter
{
    private readonly AzureBlobProtocolSettings _settings;
    private readonly SecretClient _keyVaultClient;
    private readonly ILogger<AzureBlobProtocolAdapter> _logger;
    private BlobContainerClient? _containerClient;

    public string ProtocolType => "AzureBlob";

    public AzureBlobProtocolAdapter(
        AzureBlobProtocolSettings settings,
        SecretClient keyVaultClient,
        ILogger<AzureBlobProtocolAdapter> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _keyVaultClient = keyVaultClient ?? throw new ArgumentNullException(nameof(keyVaultClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DiscoveredFileInfo>> CheckForFilesAsync(
        string serverAddress,
        string filePathPattern,
        string filenamePattern,
        string? fileExtension,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Starting Azure Blob file check on {StorageAccount}/{Container} - Prefix: {Prefix}, Pattern: {Pattern}",
            _settings.StorageAccountName,
            _settings.ContainerName,
            filePathPattern,
            filenamePattern);

        var discoveredFiles = new List<DiscoveredFileInfo>();

        try
        {
            await EnsureContainerClientAsync(cancellationToken);

            // Combine BlobPrefix from settings with filePathPattern
            var searchPrefix = CombinePrefix(_settings.BlobPrefix, filePathPattern);

            _logger.LogDebug("Listing blobs with prefix: {Prefix}", searchPrefix);

            // List blobs with the specified prefix
            await foreach (var blobItem in _containerClient!.GetBlobsAsync(
                prefix: searchPrefix,
                cancellationToken: cancellationToken))
            {
                // Extract filename from blob name (last segment of path)
                var filename = Path.GetFileName(blobItem.Name);

                // Check if filename matches pattern
                if (!MatchesPattern(filename, filenamePattern))
                {
                    continue;
                }

                // Check file extension if specified
                if (!string.IsNullOrWhiteSpace(fileExtension))
                {
                    var itemExtension = Path.GetExtension(filename).TrimStart('.');
                    if (!itemExtension.Equals(fileExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                // Build blob URL
                var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                var blobUrl = blobClient.Uri.ToString();

                var discoveredFile = new DiscoveredFileInfo
                {
                    FileUrl = blobUrl,
                    Filename = filename,
                    FileSize = blobItem.Properties.ContentLength,
                    LastModified = blobItem.Properties.LastModified,
                    DiscoveredAt = DateTimeOffset.UtcNow,
                    ProtocolMetadata = new Dictionary<string, object>
                    {
                        ["BlobName"] = blobItem.Name,
                        ["ContentType"] = blobItem.Properties.ContentType ?? "unknown",
                        ["ETag"] = blobItem.Properties.ETag?.ToString() ?? string.Empty,
                        ["BlobType"] = blobItem.Properties.BlobType?.ToString() ?? "unknown",
                        ["ContentHash"] = blobItem.Properties.ContentHash != null 
                            ? Convert.ToBase64String(blobItem.Properties.ContentHash) 
                            : string.Empty
                    }
                };

                discoveredFiles.Add(discoveredFile);

                _logger.LogDebug(
                    "Discovered Azure Blob file: {Filename} ({Size} bytes) at {Url}",
                    discoveredFile.Filename,
                    discoveredFile.FileSize,
                    discoveredFile.FileUrl);
            }

            _logger.LogInformation(
                "Azure Blob file check completed: {Count} files discovered in {Container}",
                discoveredFiles.Count,
                _settings.ContainerName);

            return discoveredFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Azure Blob file check failed on {StorageAccount}/{Container}",
                _settings.StorageAccountName,
                _settings.ContainerName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Testing Azure Blob connection to {StorageAccount}/{Container}",
                _settings.StorageAccountName,
                _settings.ContainerName);

            await EnsureContainerClientAsync(cancellationToken);

            // Try to check if container exists as a simple connectivity test
            var exists = await _containerClient!.ExistsAsync(cancellationToken);

            if (!exists.Value)
            {
                _logger.LogWarning(
                    "Azure Blob container {Container} does not exist in {StorageAccount}",
                    _settings.ContainerName,
                    _settings.StorageAccountName);
                return false;
            }

            _logger.LogInformation(
                "Azure Blob connection test successful to {StorageAccount}/{Container}",
                _settings.StorageAccountName,
                _settings.ContainerName);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Azure Blob connection test failed to {StorageAccount}/{Container}",
                _settings.StorageAccountName,
                _settings.ContainerName);
            return false;
        }
    }

    /// <summary>
    /// Ensures container client is initialized with appropriate authentication.
    /// </summary>
    private async Task EnsureContainerClientAsync(CancellationToken cancellationToken)
    {
        if (_containerClient != null)
        {
            return;
        }

        var blobServiceUri = new Uri($"https://{_settings.StorageAccountName}.blob.core.windows.net");

        switch (_settings.AuthenticationType)
        {
            case AzureAuthType.ManagedIdentity:
                // Use Managed Identity (preferred for production)
                var credential = new DefaultAzureCredential();
                var blobServiceClient = new BlobServiceClient(blobServiceUri, credential);
                _containerClient = blobServiceClient.GetBlobContainerClient(_settings.ContainerName);
                
                _logger.LogDebug(
                    "Azure Blob client created with Managed Identity for {StorageAccount}/{Container}",
                    _settings.StorageAccountName,
                    _settings.ContainerName);
                break;

            case AzureAuthType.ConnectionString:
                // Use connection string from Key Vault
                if (string.IsNullOrWhiteSpace(_settings.ConnectionStringKeyVaultSecret))
                {
                    throw new InvalidOperationException(
                        "ConnectionStringKeyVaultSecret is required for ConnectionString authentication");
                }

                var connectionString = await GetSecretAsync(
                    _settings.ConnectionStringKeyVaultSecret,
                    cancellationToken);

                var blobServiceClientWithConnStr = new BlobServiceClient(connectionString);
                _containerClient = blobServiceClientWithConnStr.GetBlobContainerClient(_settings.ContainerName);
                
                _logger.LogDebug(
                    "Azure Blob client created with Connection String for {StorageAccount}/{Container}",
                    _settings.StorageAccountName,
                    _settings.ContainerName);
                break;

            case AzureAuthType.SasToken:
                // Use SAS token from Key Vault
                if (string.IsNullOrWhiteSpace(_settings.SasTokenKeyVaultSecret))
                {
                    throw new InvalidOperationException(
                        "SasTokenKeyVaultSecret is required for SasToken authentication");
                }

                var sasToken = await GetSecretAsync(
                    _settings.SasTokenKeyVaultSecret,
                    cancellationToken);

                // Build container URI with SAS token
                var containerUriWithSas = new Uri(
                    $"{blobServiceUri}/{_settings.ContainerName}?{sasToken.TrimStart('?')}");

                _containerClient = new BlobContainerClient(containerUriWithSas);
                
                _logger.LogDebug(
                    "Azure Blob client created with SAS Token for {StorageAccount}/{Container}",
                    _settings.StorageAccountName,
                    _settings.ContainerName);
                break;

            default:
                throw new ArgumentException(
                    $"Unsupported Azure authentication type: {_settings.AuthenticationType}");
        }
    }

    /// <summary>
    /// Retrieves secret from Key Vault.
    /// </summary>
    private async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken)
    {
        return secretName;

        // Todo: Implement proper secret retrieval with error handling and caching            
        // var secret = await _keyVaultClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
        // return secret.Value.Value;
    }

    /// <summary>
    /// Combines blob prefix from settings with file path pattern.
    /// </summary>
    private string CombinePrefix(string? blobPrefix, string filePathPattern)
    {
        if (string.IsNullOrWhiteSpace(blobPrefix))
        {
            return filePathPattern.TrimStart('/');
        }

        var trimmedPrefix = blobPrefix.TrimEnd('/');
        var trimmedPath = filePathPattern.TrimStart('/');
        
        return string.IsNullOrWhiteSpace(trimmedPath) 
            ? trimmedPrefix 
            : $"{trimmedPrefix}/{trimmedPath}";
    }

    /// <summary>
    /// Matches filename against pattern with wildcard support.
    /// </summary>
    private bool MatchesPattern(string filename, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern == "*")
        {
            return true;
        }

        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            filename,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
