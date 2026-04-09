using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Encryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Security.KeyVault.Keys.Cryptography;
using System.Net;

namespace RiskInsure.FileProcessing.Infrastructure.Cosmos;

/// <summary>
/// T034: Cosmos DB context for file processing service.
/// Provides container initialization and client management with Always Encrypted support.
/// </summary>
public class CosmosDbContext
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly ILogger<CosmosDbContext> _logger;
    private readonly CosmosEncryptionConfiguration _encryptionConfig;
    private readonly SemaphoreSlim InitializationLock = new SemaphoreSlim(1, 1);
    private bool _databaseInitialized = false;
    private bool _dekInitialized = false;

    public Database Database { get; private set; } = null!;
    public Container ConfigurationsContainer { get; private set; } = null!;
    public Container ExecutionsContainer { get; private set; } = null!;
    public Container DiscoveredFilesContainer { get; private set; } = null!;
    public Container ProcessedFilesContainer { get; private set; } = null!;

    private const string configsContainerName = "file-processing-configurations";
    private const string executionsContainerName = "file-processing-executions";
    private const string discoveredFilesContainerName = "file-processing-discovered-files";
    private const string processedFilesContainerName = "file-processing-processed-files";

    public CosmosDbContext(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<CosmosDbContext> logger,
        CosmosEncryptionConfiguration encryptionConfig)
    {
        _cosmosClient = cosmosClient;
        _databaseName = configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
        _logger = logger;
        _encryptionConfig = encryptionConfig;
    }

    /// <summary>
    /// Initialize database and containers with encryption policies
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing Cosmos DB context for database: {DatabaseName}", _databaseName);

        // Get database reference
        Database = _cosmosClient.GetDatabase(_databaseName);

        // Initialize encryption configuration (validates Key Vault access)
        // This may fail if Key Vault is misconfigured, but we allow graceful degradation
        try
        {
            await _encryptionConfig.InitializeEncryptionAsync(cancellationToken);
            _logger.LogInformation("✓ Encryption configuration initialized successfully");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(
                "Encryption configuration failed (service will continue without encryption): {ErrorMessage}. " +
                "Verify Key Vault URI and credentials are properly configured in appsettings.encryption.json",
                ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during encryption initialization");
            throw;  // Fatal errors still crash startup
        }

        EncryptionPolicyMetadata? configsEncryptionMetadata = null;
        try
        {
            configsEncryptionMetadata = await _encryptionConfig.GetEncryptionPolicyMetadataForFileProcessingConfigs(cancellationToken);
            _logger.LogInformation(
                "Encryption configured for {PathCount} sensitive properties: {Paths}",
                configsEncryptionMetadata.EncryptionPaths.Count,
                string.Join(", ", configsEncryptionMetadata.EncryptionPaths));
        }
        catch (InvalidOperationException)
        {
            _logger.LogInformation("Encryption metadata unavailable - configuration container will be created without encryption policy");
        }

        await EnsureDbAndContainerAsync(configsContainerName, "/clientId", configsEncryptionMetadata, cancellationToken);
        await EnsureDbAndContainerAsync(executionsContainerName, "/clientId", null, cancellationToken);
        await EnsureDbAndContainerAsync(discoveredFilesContainerName, "/clientId", null, cancellationToken);
        await EnsureDbAndContainerAsync(processedFilesContainerName, "/clientId", null, cancellationToken);

        // Get container references
        ConfigurationsContainer = Database.GetContainer(configsContainerName);
        ExecutionsContainer = Database.GetContainer(executionsContainerName);
        DiscoveredFilesContainer = Database.GetContainer(discoveredFilesContainerName);
        ProcessedFilesContainer = Database.GetContainer(processedFilesContainerName);

        _logger.LogInformation("Cosmos DB context initialized successfully");
    }

    private async Task EnsureDbAndContainerAsync(
        string containerName,
        string partitionKeyPath,
        EncryptionPolicyMetadata? encryptionMetadata,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKeyPath);

        await InitializationLock.WaitAsync(cancellationToken);
        try
        {
            if (!_databaseInitialized)
            {
                await ExecuteWithRetryAsync(
                    async ct => await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName, cancellationToken: ct),
                    $"create database {_databaseName}",
                    cancellationToken);

                _databaseInitialized = true;
            }

            var database = _cosmosClient.GetDatabase(_databaseName);

            // Single DEK shared across containers for simplicity, 
            // but could be per-container if needed
            if (!_dekInitialized && encryptionMetadata != null) 
            {
                try
                {
                    ClientEncryptionKeyProperties dekProperties = await ExecuteWithRetryAsync(
                        async ct => await database.CreateClientEncryptionKeyAsync(
                            clientEncryptionKeyId: encryptionMetadata.DataEncryptionKeyName,
                            encryptionAlgorithm: DataEncryptionAlgorithm.AeadAes256CbcHmacSha256,
                            encryptionKeyWrapMetadata: new EncryptionKeyWrapMetadata(
                                type: KeyEncryptionKeyResolverName.AzureKeyVault,
                                name: "name of cmk?",
                                value: encryptionMetadata.CmkKeyId,
                                algorithm: EncryptionAlgorithm.RsaOaep.ToString())),
                        $"create DEK {encryptionMetadata.DataEncryptionKeyName}",
                        ct: cancellationToken);
                    _logger.LogInformation("DEK Key created: " + dekProperties.Id);
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogInformation("DEK key already exists.");
                }
                _dekInitialized = true;
            }

            var containerProperties = new ContainerProperties
            {
                Id = containerName,
                PartitionKeyPath = partitionKeyPath,
                DefaultTimeToLive = -1
            };

            if (encryptionMetadata != null)
            {
                TryApplyEncryptionPolicy(containerProperties, encryptionMetadata, containerName);
            }

            await ExecuteWithRetryAsync(
                async ct => await database.CreateContainerIfNotExistsAsync(containerProperties, cancellationToken: ct),
                $"create container {containerName}",
                cancellationToken);
        }
        finally
        {
            InitializationLock.Release();
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<CancellationToken, Task<T>> action, string operationName, CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(1);
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                return await action(ct);
            }
            catch (CosmosException ex) when (IsTransient(ex) && attempt < 20)
            {
                _logger.LogWarning(ex, "Transient Cosmos failure during {Operation} (attempt {Attempt}/20). Retrying in {DelayMs}ms.", operationName, attempt, (int)delay.TotalMilliseconds);
                await Task.Delay(delay, ct);
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 15000));
            }
        }

        return await action(ct);
    }

    private static bool IsTransient(CosmosException ex) =>
        ex.StatusCode == HttpStatusCode.ServiceUnavailable
        || ex.StatusCode == HttpStatusCode.RequestTimeout
        || ex.StatusCode == HttpStatusCode.TooManyRequests
        || ex.StatusCode == HttpStatusCode.InternalServerError
        || ex.StatusCode == HttpStatusCode.BadGateway
        || ex.StatusCode == HttpStatusCode.GatewayTimeout;

    private void TryApplyEncryptionPolicy(
        ContainerProperties containerProperties,
        EncryptionPolicyMetadata encryptionMetadata,
        string containerName)
    {
        try
        {
            var includedPaths = encryptionMetadata.EncryptionPaths
                .Select(path => new ClientEncryptionIncludedPath
                {
                    Path = path,
                    ClientEncryptionKeyId = encryptionMetadata.DataEncryptionKeyName,
                    EncryptionType = "Randomized",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256"
                })
                .ToList();

            containerProperties.ClientEncryptionPolicy = new ClientEncryptionPolicy(includedPaths);

            _logger.LogInformation(
                "Applied encryption policy to container {ContainerName} for {PathCount} paths",
                containerName,
                encryptionMetadata.EncryptionPaths.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to apply encryption policy to container {ContainerName}; proceeding without encryption policy",
                containerName);
        }
    }
}
