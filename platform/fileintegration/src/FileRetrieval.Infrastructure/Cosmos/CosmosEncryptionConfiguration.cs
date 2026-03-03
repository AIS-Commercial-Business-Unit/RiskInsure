using System.Runtime.CompilerServices;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
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

    private readonly ILogger<CosmosEncryptionConfiguration> _logger;
    private readonly SecretClient _secretClient;

    private string? encryptionKeyName;

    public CosmosEncryptionConfiguration(
        IConfiguration configuration,
        SecretClient secretClient,
        ILogger<CosmosEncryptionConfiguration> logger)
    {
        _logger = logger;
        _secretClient = secretClient;

        _encryptionKeyName = configuration["CosmosDb:EncryptionKeyName"] ?? "file-retrieval-dek";
    }

    /// <summary>
    /// Initializes the Key Vault client for encryption key management.
    /// Validates that Key Vault is accessible and encryption keys exist.
    /// </summary>
    public async Task InitializeEncryptionAsync()
    {
        _logger.LogInformation("Initializing CosmosDB Always Encrypted configuration");

        try
        {
            // Validate Key Vault connectivity and key accessibility
            _logger.LogInformation("Validating Key Vault access and encryption key availability");

            var key = _secretClient.GetSecret(_encryptionKeyName);
            _logger.LogInformation("Encryption key found: {KeyName} (ID: {KeyId})", _encryptionKeyName, key.Value.Id);

            _logger.LogInformation("CosmosDB Always Encrypted initialized successfully with Key Vault");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize CosmosDB encryption with Key Vault. Ensure Key Vault URI and credentials are configured correctly.");
            throw;
        }
    }

    /// <summary>
    /// Returns configuration metadata for encryption policy.
    /// This information is used to configure container-level encryption policies.
    /// </summary>
    public async Task<EncryptionPolicyMetadata> GetEncryptionPolicyMetadata()
    {
        var key = await _secretClient.GetSecretAsync(_encryptionKeyName);
        _logger.LogDebug("Retrieved encryption key for policy metadata: {KeyName} (ID: {KeyId})", _encryptionKeyName, key.Value.Id);

        return new EncryptionPolicyMetadata
        {
            DataEncryptionKey = key.Value,
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
    public string DataEncryptionKeyId { get; set; } = null!;
    public List<string> EncryptionPaths { get; set; } = new();
}

