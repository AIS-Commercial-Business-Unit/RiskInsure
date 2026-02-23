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

    public Database Database { get; private set; } = null!;
    public Container ConfigurationsContainer { get; private set; } = null!;
    public Container ExecutionsContainer { get; private set; } = null!;
    public Container DiscoveredFilesContainer { get; private set; } = null!;

    public CosmosDbContext(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<CosmosDbContext> logger)
    {
        _cosmosClient = cosmosClient;
        _databaseName = configuration["CosmosDb:DatabaseName"] 
            ?? throw new InvalidOperationException("CosmosDb:DatabaseName configuration is missing");
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

        // Get container references
        ConfigurationsContainer = Database.GetContainer("file-retrieval-configurations");
        ExecutionsContainer = Database.GetContainer("file-retrieval-executions");
        DiscoveredFilesContainer = Database.GetContainer("discovered-files");

        _logger.LogInformation("Cosmos DB context initialized successfully");
    }
}
