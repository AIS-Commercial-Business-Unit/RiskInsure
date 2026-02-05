using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Infrastructure
{
    /// <summary>
    /// Initializes Cosmos DB database and containers on application startup.
    /// Ensures all required resources exist before the application processes requests.
    /// 
    /// For FREE TIER (1000 RU/s): Use databaseThroughput=1000 to share across ALL containers.
    /// This allows unlimited containers sharing the 1000 RU/s instead of 400 RU/s per container (max 2).
    /// </summary>
    public static class CosmosDbInitializer
    {
        private static bool _databaseInitialized = false;
        private static readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Creates database with shared throughput and container.
        /// </summary>
        /// <param name="client">Cosmos DB client</param>
        /// <param name="dbName">Database name</param>
        /// <param name="containerName">Container name</param>
        /// <param name="partitionKeyPath">Partition key path (e.g., "/accountId")</param>
        /// <param name="databaseThroughput">Database-level throughput (RU/s) shared across ALL containers. Use 1000 for free tier. Pass null for serverless.</param>
        public static async Task EnsureDbAndContainerAsync(
            CosmosClient client, 
            string dbName, 
            string containerName, 
            string partitionKeyPath,
            int? databaseThroughput = 1000)
        {
            await _initLock.WaitAsync();
            try
            {
                // Create database with shared throughput if it doesn't exist
                // IMPORTANT: Database-level throughput allows unlimited containers to share RU/s
                // Free tier: 1000 RU/s shared across all containers (not 400 RU/s per container)
                if (!_databaseInitialized)
                {
                    DatabaseResponse dbResponse;
                    if (databaseThroughput.HasValue)
                    {
                        // Shared throughput at database level
                        dbResponse = await client.CreateDatabaseIfNotExistsAsync(dbName, ThroughputProperties.CreateManualThroughput(databaseThroughput.Value));
                    }
                    else
                    {
                        // Serverless (no throughput)
                        dbResponse = await client.CreateDatabaseIfNotExistsAsync(dbName);
                    }
                    _databaseInitialized = true;
                }

                var database = client.GetDatabase(dbName);
                
                // Create container WITHOUT specifying throughput - it inherits from database
                // This allows sharing the database's 1000 RU/s across all containers
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
                
                // CRITICAL: Do NOT specify throughput here - container inherits from database
                await database.CreateContainerIfNotExistsAsync(containerProperties);
            }
            finally
            {
                _initLock.Release();
            }
        }
    }
}