namespace RiskInsure.Billing.Infrastructure;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class CosmosDbInitializer
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CosmosDbInitializer> _logger;

    public CosmosDbInitializer(IConfiguration configuration, ILogger<CosmosDbInitializer> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Container> InitializeAsync()
    {
        var connectionString = _configuration.GetConnectionString("CosmosDb");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("CosmosDb connection string is not configured");
        }

        var client = new CosmosClient(connectionString);
        var databaseName = "BillingDb";
        var containerName = "Billing";

        _logger.LogInformation("Initializing Cosmos DB database {DatabaseName}", databaseName);

        var database = await client.CreateDatabaseIfNotExistsAsync(databaseName);
        
        _logger.LogInformation("Initializing Cosmos DB container {ContainerName}", containerName);

        var containerProperties = new ContainerProperties
        {
            Id = containerName,
            PartitionKeyPath = "/orderId"
        };

        var container = await database.Database.CreateContainerIfNotExistsAsync(containerProperties);

        _logger.LogInformation("Cosmos DB initialized successfully");

        return container.Container;
    }
}
