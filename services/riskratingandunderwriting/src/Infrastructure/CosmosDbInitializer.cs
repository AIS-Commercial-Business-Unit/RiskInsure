namespace RiskInsure.RiskRatingAndUnderwriting.Infrastructure;

using Microsoft.Azure.Cosmos;

public static class CosmosDbInitializer
{
    public static async Task EnsureDbAndContainerAsync(
        CosmosClient cosmosClient,
        string databaseName,
        string containerName,
        string partitionKeyPath)
    {
        var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);

        var containerProperties = new ContainerProperties
        {
            Id = containerName,
            PartitionKeyPath = partitionKeyPath
        };

        await database.Database.CreateContainerIfNotExistsAsync(containerProperties);
    }
}
