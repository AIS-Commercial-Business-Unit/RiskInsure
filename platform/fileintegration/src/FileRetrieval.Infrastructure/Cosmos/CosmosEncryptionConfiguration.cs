using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RiskInsure.FileRetrieval.Infrastructure.Cosmos;

/// <summary>
/// Manages CosmosDB Always Encrypted configuration for sensitive properties.
/// Handles encryption policy setup and key management through Azure Key Vault.
/// 
/// Note: This implementation prepares the infrastructure for client-side encryption.
/// The actual encryption is handled at the SDK level when Always Encrypted support is available.
/// </summary>
public class CosmosEncryptionConfiguration
{
    // In order for properties to be encrypted with Always Encrypted, 
    // they have to reside at the root of the JSON being sent to 
    // Cosmos. The JsonPath attribute is a hint for the converter to 
    // find them so that they can be mapped to the deeper properties
    // in our model classes.
    // 
    // WARNING: You can't change these values without creating a new 
    // CosmosDbContainer and migrating all of your data,
    // because CosmosDB doesn't support changing encyption paths after
    // a container has been created.  
    public const string FtpSecretPath = "/ftpProtocolSettings_password";
    public const string HttpsSecretPath = "/httpsProtocolSettings_passwordOrTokenOrApiKey";
    public const string AzureBlobSecretPath = "/azureBlobSettings_connectionString";

    private readonly IConfiguration _configuration;
    private readonly ILogger<CosmosEncryptionConfiguration> _logger;
    private KeyClient? _keyClient;
    private string? _resolvedDataEncryptionKeyId;
    private string? _resolvedKeyVaultKeyUri;

    public CosmosEncryptionConfiguration(
        IConfiguration configuration,
        ILogger<CosmosEncryptionConfiguration> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Initializes the Key Vault client for encryption key management.
    /// Validates that Key Vault is accessible and encryption keys exist.
    /// </summary>
    public async Task InitializeEncryptionAsync()
    {
        _logger.LogInformation("Initializing CosmosDB Always Encrypted configuration");

        var keyVaultUri = _configuration["AzureKeyVault:VaultUri"]
            ?? throw new InvalidOperationException("AzureKeyVault:VaultUri configuration is required for encryption setup");

        var keyVaultKeyUri = _configuration["CosmosDb:Encryption:KeyVaultKeyUri"]
            ?? throw new InvalidOperationException("CosmosDb:Encryption:KeyVaultKeyUri configuration is required for encryption setup");

        var dataEncryptionKeyId = _configuration["CosmosDb:Encryption:DataEncryptionKeyId"] ?? "file-retrieval-dek";

        try
        {
            // Initialize Key Vault client with Azure Identity
            _keyClient = new KeyClient(
                new Uri(keyVaultUri),
                new DefaultAzureCredential());

            // Validate Key Vault connectivity and key accessibility
            _logger.LogInformation("Validating Key Vault access and encryption key availability");
            await ValidateKeyVaultConnectivityAsync(keyVaultKeyUri);

            var resolvedKey = await RetrieveKeyByDataEncryptionKeyIdAsync(dataEncryptionKeyId);
            _resolvedDataEncryptionKeyId = resolvedKey.Name;
            _resolvedKeyVaultKeyUri = resolvedKey.Id?.ToString();

            _logger.LogInformation(
                "Resolved encryption key from DataEncryptionKeyId {ConfiguredKeyId} to Key Vault key {ResolvedKeyName}",
                dataEncryptionKeyId,
                _resolvedDataEncryptionKeyId);

            _logger.LogInformation("CosmosDB Always Encrypted initialized successfully with Key Vault");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize CosmosDB encryption with Key Vault. Ensure Key Vault URI and credentials are configured correctly.");
            throw;
        }
    }

    private async Task<KeyVaultKey> RetrieveKeyByDataEncryptionKeyIdAsync(string dataEncryptionKeyId)
    {
        if (_keyClient == null)
        {
            throw new InvalidOperationException("Key Vault client not initialized");
        }

        try
        {
            if (Uri.TryCreate(dataEncryptionKeyId, UriKind.Absolute, out var dataEncryptionKeyUri))
            {
                var pathSegments = dataEncryptionKeyUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (pathSegments.Length >= 2 && pathSegments[0] == "keys")
                {
                    var keyName = pathSegments[1];
                    var keyVersion = pathSegments.Length > 2 ? pathSegments[2] : null;

                    var keyByUri = keyVersion != null
                        ? await _keyClient.GetKeyAsync(keyName, keyVersion)
                        : await _keyClient.GetKeyAsync(keyName);

                    return keyByUri.Value;
                }
            }

            var keyByName = await _keyClient.GetKeyAsync(dataEncryptionKeyId);
            return keyByName.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve Key Vault key using DataEncryptionKeyId {DataEncryptionKeyId}",
                dataEncryptionKeyId);
            throw;
        }
    }

    /// <summary>
    /// Validates that the Key Vault is accessible and the encryption key exists.
    /// </summary>
    private async Task ValidateKeyVaultConnectivityAsync(string keyVaultKeyUri)
    {
        if (_keyClient == null)
        {
            throw new InvalidOperationException("Key Vault client not initialized");
        }

        try
        {
            // Parse key URI to get key name and optional version
            // Format: https://{vault}.vault.azure.net/keys/{name} or https://{vault}.vault.azure.net/keys/{name}/{version}
            var uri = new Uri(keyVaultKeyUri);
            var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            if (pathSegments.Length < 2 || pathSegments[0] != "keys")
            {
                throw new InvalidOperationException(
                    $"Invalid Key Vault key URI format. Expected: https://{{vault}}.vault.azure.net/keys/{{name}} or " +
                    $"https://{{vault}}.vault.azure.net/keys/{{name}}/{{version}}. Got: {keyVaultKeyUri}");
            }

            var keyName = pathSegments[1];
            var keyVersion = pathSegments.Length > 2 ? pathSegments[2] : null;

            // Attempt to retrieve the key
            var key = keyVersion != null
                ? await _keyClient.GetKeyAsync(keyName, keyVersion)
                : await _keyClient.GetKeyAsync(keyName);

            _logger.LogInformation("Encryption key validated: {KeyName} (ID: {KeyId})", keyName, key.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Key Vault validation failed. Ensure the encryption key exists and you have access permissions.");
            throw;
        }
    }

    /// <summary>
    /// Returns configuration metadata for encryption policy.
    /// This information is used to configure container-level encryption policies.
    /// </summary>
    public EncryptionPolicyMetadata GetEncryptionPolicyMetadata()
    {
        var configuredDataEncryptionKeyId = _configuration["CosmosDb:Encryption:DataEncryptionKeyId"] ?? "file-retrieval-dek";
        var configuredKeyVaultKeyUri = _configuration["CosmosDb:Encryption:KeyVaultKeyUri"]
            ?? throw new InvalidOperationException("CosmosDb:Encryption:KeyVaultKeyUri configuration missing");

        return new EncryptionPolicyMetadata
        {
            KeyVaultUri = _configuration["AzureKeyVault:VaultUri"]
                ?? throw new InvalidOperationException("AzureKeyVault:VaultUri configuration missing"),
            KeyVaultKeyUri = _resolvedKeyVaultKeyUri ?? configuredKeyVaultKeyUri,
            DataEncryptionKeyId = _resolvedDataEncryptionKeyId ?? configuredDataEncryptionKeyId,
            EncryptionPaths = new List<string>
            {
                FtpSecretPath,
                HttpsSecretPath,
                AzureBlobSecretPath
            }
        };
    }
}

/// <summary>
/// Metadata for encryption policy configuration.
/// </summary>
public class EncryptionPolicyMetadata
{
    public string KeyVaultUri { get; set; } = null!;
    public string KeyVaultKeyUri { get; set; } = null!;
    public string DataEncryptionKeyId { get; set; } = null!;
    public List<string> EncryptionPaths { get; set; } = new();
}

