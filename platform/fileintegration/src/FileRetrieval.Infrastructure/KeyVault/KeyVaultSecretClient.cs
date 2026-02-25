using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RiskInsure.FileRetrieval.Infrastructure.KeyVault;

/// <summary>
/// T039: Azure Key Vault client wrapper for retrieving secrets.
/// </summary>
public class KeyVaultSecretClient
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<KeyVaultSecretClient> _logger;

    public KeyVaultSecretClient(
        IConfiguration configuration,
        ILogger<KeyVaultSecretClient> logger)
    {
        var vaultUri = configuration["AzureKeyVault:VaultUri"]
            ?? throw new InvalidOperationException("AzureKeyVault:VaultUri configuration is missing");

        _secretClient = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
        _logger = logger;
    }

    /// <summary>
    /// Retrieve a secret value from Key Vault
    /// </summary>
    public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Retrieving secret '{SecretName}' from Key Vault", secretName);

            var secret = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            
            return secret.Value.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogError("Secret '{SecretName}' not found in Key Vault", secretName);
            throw new InvalidOperationException($"Secret '{secretName}' not found in Key Vault", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret '{SecretName}' from Key Vault", secretName);
            throw;
        }
    }
}
