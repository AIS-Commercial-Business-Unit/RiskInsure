namespace Infrastructure;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class CosmosDbInitializer
{
    private readonly CosmosClient _cosmosClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CosmosDbInitializer> _logger;

    public CosmosDbInitializer(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<CosmosDbInitializer> logger)
    {
        _cosmosClient = cosmosClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Container> InitializeAsync()
    {
        var databaseName = "RiskInsure";
        var containerName = "ratingunderwriting";

        var database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
        _logger.LogInformation("Database {DatabaseName} ready", databaseName);

        var containerProperties = new ContainerProperties
        {
            Id = containerName,
            PartitionKeyPath = "/quoteId"
        };

        var container = await database.Database.CreateContainerIfNotExistsAsync(
            containerProperties,
            throughput: 400);

        _logger.LogInformation(
            "Container {ContainerName} ready with partition key {PartitionKey}",
            containerName, containerProperties.PartitionKeyPath);

        return container.Container;
    }
}
