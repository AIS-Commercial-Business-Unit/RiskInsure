using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Security.KeyVault.Keys;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RiskInsure.FileProcessing.Domain.Serialization;

namespace RiskInsure.FileProcessing.Infrastructure.Cosmos;

/// <summary>
/// Manages CosmosDB Always Encrypted configuration for sensitive properties.
/// Handles encryption policy setup and key management through Azure Key Vault.
/// 
/// Note: This implementation prepares the infrastructure for client-side encryption.
/// The actual encryption is handled at the SDK level when Always Encrypted support is available.
/// </summary>
public class CosmosEncryptionConfiguration
{
    private readonly ILogger<CosmosEncryptionConfiguration> _logger;
    private readonly SecretClient _secretClient;
    private readonly KeyClient _keyClient;
    private readonly string _cmkKeyName;
    private readonly string _dekKeyName;

    private string? _cmkKeyId = null;

    private readonly bool _usingKeyVaultEmulator;
    private readonly string _keyVaultUri;

    public CosmosEncryptionConfiguration(
        IConfiguration configuration,
        SecretClient secretClient,
        KeyClient keyClient,
        ILogger<CosmosEncryptionConfiguration> logger)
    {
        _logger = logger;
        _secretClient = secretClient;
        _keyClient = keyClient;

        _cmkKeyName = configuration["CosmosDb:CmkKeyName"] ?? "file-processing-cmk";
        _dekKeyName = configuration["CosmosDb:DekKeyName"] ?? "file-processing-dek";
        _usingKeyVaultEmulator = configuration["AzureKeyVault:UsingEmulator"] != null &&
                                 bool.TryParse(configuration["AzureKeyVault:UsingEmulator"], out var usingEmulator) &&
                                 usingEmulator;
        _keyVaultUri = configuration["AzureKeyVault:VaultUri"] ?? throw new InvalidOperationException("AzureKeyVault:VaultUri configuration is missing");
    }

    /// <summary>
    /// Initializes the Key Vault client for encryption key management.
    /// Validates that Key Vault is accessible and encryption keys exist.
    /// </summary>
    public async Task InitializeEncryptionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing CosmosDB Always Encrypted configuration");

        try
        {
            // Setup sample RSA CMK key if we're using the key vault emulator, to 
            // ensure encryption works out of the box in development environments
            if (_usingKeyVaultEmulator)
            {
                var rsaOptions = new CreateRsaKeyOptions(_cmkKeyName)
                {
                    KeySize = 2048,
                    ExpiresOn = DateTimeOffset.UtcNow.AddYears(1), // Key expires in one year
                    Enabled = true
                };
                rsaOptions.KeyOperations.Add(KeyOperation.Encrypt);
                rsaOptions.KeyOperations.Add(KeyOperation.Decrypt);
                rsaOptions.KeyOperations.Add(KeyOperation.Sign);
                rsaOptions.KeyOperations.Add(KeyOperation.Verify);

                KeyVaultKey rsaKey = await _keyClient.CreateRsaKeyAsync(rsaOptions);
                _cmkKeyId = rsaKey.Id.ToString();
                _logger.LogInformation("Created RSA CMK key in Key Vault emulator: {KeyName} (ID: {KeyId})", _cmkKeyName, _cmkKeyId);
            }
            else
            {
                // In production, we expect the CMK to already exist in Key Vault and just retrieve its ID
                KeyVaultKey rsaKey = await _keyClient.GetKeyAsync(_cmkKeyName, cancellationToken: cancellationToken);
                _cmkKeyId = rsaKey.Id.ToString();
                _logger.LogInformation("Retrieved RSA CMK key from Key Vault: {KeyName} (ID: {KeyId})", _cmkKeyName, _cmkKeyId);
            }

            _logger.LogInformation("Cosmos DB Encryption Initialization Complete");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize CosmosDB encryption with Key Vault. Service will continue without encryption policy.");
            throw new InvalidOperationException("CosmosDB encryption initialization failed", ex);
        }
    }

    /// <summary>
    /// Returns configuration metadata for encryption policy for File Processing Config container.
    /// </summary>
    public async Task<EncryptionPolicyMetadata> GetEncryptionPolicyMetadataForFileProcessingConfigs(CancellationToken cancellationToken = default)
    {
        return new EncryptionPolicyMetadata
        {
            CmkKeyId = _cmkKeyId ?? throw new InvalidOperationException("CMK Key ID is not initialized"),
            DataEncryptionKeyName = _dekKeyName,
            EncryptionPaths = new List<string>
            {
                SecretPaths.FtpSecretPath,
                SecretPaths.HttpsSecretPath,
                SecretPaths.AzureBlobSecretPath
            }
        };
    }
}

/// <summary>
/// Metadata for encryption policy configuration.
/// </summary>
public class EncryptionPolicyMetadata
{
    public string CmkKeyId { get; set; } = null!;
    public string DataEncryptionKeyName { get; set; } = null!;
    public List<string> EncryptionPaths { get; set; } = new();
}

