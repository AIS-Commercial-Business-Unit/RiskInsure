using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Infrastructure
{
    /// <summary>
    /// Initializes Cosmos DB database and containers on application startup.
    /// Ensures all required resources exist before the application processes requests.
    /// </summary>
    public static class CosmosDbInitializer
    {
        /// <summary>
        /// Creates database and container if they don't exist.
        /// </summary>
        /// <param name="client">Cosmos DB client</param>
        /// <param name="dbName">Database name</param>
        /// <param name="containerName">Container name</param>
        /// <param name="partitionKeyPath">Partition key path (e.g., "/accountId")</param>
        /// <param name="throughput">Optional throughput (RU/s). Pass null for serverless accounts. Default is 400 RU/s for provisioned accounts.</param>
        public static async Task EnsureDbAndContainerAsync(
            CosmosClient client, 
            string dbName, 
            string containerName, 
            string partitionKeyPath,
            int? throughput = 400)
        {
            // Create database if it doesn't exist
            // For serverless accounts, don't specify throughput (pass null)
            DatabaseResponse dbResponse;
            if (throughput.HasValue)
            {
                dbResponse = await client.CreateDatabaseIfNotExistsAsync(dbName, throughput: throughput.Value);
            }
            else
            {
                dbResponse = await client.CreateDatabaseIfNotExistsAsync(dbName);
            }
            
            // Create container if it doesn't exist with optimized indexing policy
            var containerProperties = new ContainerProperties
            {
                Id = containerName,
                PartitionKeyPath = partitionKeyPath,
                DefaultTimeToLive = -1, // Enable TTL but don't auto-delete (use per-document TTL)
                IndexingPolicy = new IndexingPolicy
                {
                    Automatic = true,
                    IndexingMode = IndexingMode.Consistent,
                    IncludedPaths =
                    {
                        // Only index fields we actually query on
                        new IncludedPath { Path = "/type/?" },
                        new IncludedPath { Path = "/customerId/?" },
                        new IncludedPath { Path = "/policyNumber/?" },
                        new IncludedPath { Path = "/status/?" }
                    },
                    ExcludedPaths =
                    {
                        // Exclude everything else to speed up writes
                        new ExcludedPath { Path = "/*" }
                    }
                }
            };
            
            var containerResponse = await dbResponse.Database.CreateContainerIfNotExistsAsync(containerProperties);
        }
    }
}