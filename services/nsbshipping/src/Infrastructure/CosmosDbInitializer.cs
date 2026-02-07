namespace RiskInsure.NsbShipping.Infrastructure;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class CosmosDbInitializer
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CosmosDbInitializer> _logger;
    public CosmosDbInitializer(IConfiguration configuration, ILogger<CosmosDbInitializer> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Container> InitializeAsync()
    {
        var connectionString = _configuration.GetConnectionString("CosmosDb")!;
        var client = new CosmosClient(connectionString);
        var db = await client.CreateDatabaseIfNotExistsAsync("NsbShippingDb");
        var container = await db.Database.CreateContainerIfNotExistsAsync(
            id: "NsbShipping",
            partitionKeyPath: "/orderId",
            throughput: 400);
        _logger.LogInformation("Cosmos DB container initialized: {ContainerId}", container.Container.Id);
        return container.Container;
    }
}
