using Microsoft.Azure.Cosmos;

namespace Infrastructure;

public static class CosmosDbInitializer
{
    public static async Task EnsureDbAndContainerAsync(
        CosmosClient client,
        string dbName,
        string containerName,
        string partitionKeyPath,
        int? throughput = 400)
    {
        var dbResponse = await client.CreateDatabaseIfNotExistsAsync(
            dbName,
            throughput: throughput);

        await dbResponse.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties
            {
                Id = containerName,
                PartitionKeyPath = partitionKeyPath,
                DefaultTimeToLive = -1
            });
    }
}
