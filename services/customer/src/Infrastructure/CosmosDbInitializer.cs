namespace RiskInsure.Customer.Infrastructure;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

public class CosmosDbInitializer
{
    private readonly CosmosClient _cosmosClient;
    private readonly ILogger<CosmosDbInitializer> _logger;
    private const string DatabaseName = "RiskInsure";
    private const string ContainerName = "customer";

    public CosmosDbInitializer(CosmosClient cosmosClient, ILogger<CosmosDbInitializer> logger)
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Container> InitializeAsync()
    {
        _logger.LogInformation("Initializing Cosmos DB database {DatabaseName}", DatabaseName);

        var database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseName);
        
        _logger.LogInformation("Creating container {ContainerName} with partition key /customerId", ContainerName);

        var containerResponse = await database.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties
            {
                Id = ContainerName,
                PartitionKeyPath = "/customerId"
            });

        _logger.LogInformation("Cosmos DB initialization complete");

        return containerResponse.Container;
    }
}
