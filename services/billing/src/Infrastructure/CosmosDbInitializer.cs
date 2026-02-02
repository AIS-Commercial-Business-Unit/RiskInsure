using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Infrastructure
{
    public static class CosmosDbInitializer
    {
        public static async Task EnsureDbAndContainerAsync(CosmosClient client, string dbName, string containerName, string partitionKeyPath)
        {
            var dbResponse = await client.CreateDatabaseIfNotExistsAsync(dbName);
            await dbResponse.Database.CreateContainerIfNotExistsAsync(new ContainerProperties
            {
                Id = containerName,
                PartitionKeyPath = partitionKeyPath
            });
        }
    }
}