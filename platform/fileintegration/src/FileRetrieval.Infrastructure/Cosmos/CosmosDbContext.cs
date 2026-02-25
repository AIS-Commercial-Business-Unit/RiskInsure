using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RiskInsure.FileRetrieval.Infrastructure.Cosmos;

/// <summary>
/// T034: Cosmos DB context for file retrieval service.
/// Provides container initialization and client management.
/// </summary>
public class CosmosDbContext
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly ILogger<CosmosDbContext> _logger;
    private readonly SemaphoreSlim InitializationLock = new SemaphoreSlim(1, 1);
    private bool _databaseInitialized = false;

    public Database Database { get; private set; } = null!;
    public Container ConfigurationsContainer { get; private set; } = null!;
    public Container ExecutionsContainer { get; private set; } = null!;
    public Container DiscoveredFilesContainer { get; private set; } = null!;

    private const string configsContainerName = "file-retrieval-configurations";
    private const string executionsContainerName = "file-retrieval-executions";
    private const string discoveredFilesContainerName = "file-retrieval-discovered-files";

    public CosmosDbContext(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<CosmosDbContext> logger)
    {
        _cosmosClient = cosmosClient;
        _databaseName = configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
        _logger = logger;
    }

    /// <summary>
    /// Initialize database and containers
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing Cosmos DB context for database: {DatabaseName}", _databaseName);

        // Get database reference
        Database = _cosmosClient.GetDatabase(_databaseName);

        await EnsureDbAndContainerAsync(configsContainerName, "/clientId", cancellationToken);
        await EnsureDbAndContainerAsync(executionsContainerName, "/clientId", cancellationToken);
        await EnsureDbAndContainerAsync(discoveredFilesContainerName, "/clientId", cancellationToken);

        // Get container references
        ConfigurationsContainer = Database.GetContainer(configsContainerName);
        ExecutionsContainer = Database.GetContainer(executionsContainerName);
        DiscoveredFilesContainer = Database.GetContainer(discoveredFilesContainerName);

        _logger.LogInformation("Cosmos DB context initialized successfully");
    }

    private async Task EnsureDbAndContainerAsync(
        string containerName,
        string partitionKeyPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKeyPath);

        await InitializationLock.WaitAsync(cancellationToken);
        try
        {
            if (!_databaseInitialized)
            {
                await _cosmosClient.CreateDatabaseIfNotExistsAsync(
                    _databaseName,
                    cancellationToken: cancellationToken);

                _databaseInitialized = true;
            }

            var database = _cosmosClient.GetDatabase(_databaseName);

            var containerProperties = new ContainerProperties
            {
                Id = containerName,
                PartitionKeyPath = partitionKeyPath,
                DefaultTimeToLive = -1
            };

            await database.CreateContainerIfNotExistsAsync(
                containerProperties,
                cancellationToken: cancellationToken);
        }
        finally
        {
            InitializationLock.Release();
        }
    }
}
