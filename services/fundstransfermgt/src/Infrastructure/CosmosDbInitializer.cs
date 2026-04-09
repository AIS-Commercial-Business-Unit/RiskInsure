using Microsoft.Azure.Cosmos;

namespace RiskInsure.FundTransferMgt.Infrastructure;

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
    /// <param name="partitionKeyPath">Partition key path (e.g., "/customerId")</param>
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
            if (!_databaseInitialized)
            {
                var dbDelay = TimeSpan.FromSeconds(1);
                for (var attempt = 1; attempt <= 20; attempt++)
                {
                    try
                    {
                        if (databaseThroughput.HasValue)
                            await client.CreateDatabaseIfNotExistsAsync(dbName, ThroughputProperties.CreateManualThroughput(databaseThroughput.Value));
                        else
                            await client.CreateDatabaseIfNotExistsAsync(dbName);
                        break;
                    }
                    catch (CosmosException ex) when (IsTransient(ex) && attempt < 20)
                    {
                        await Task.Delay(dbDelay);
                        dbDelay = TimeSpan.FromMilliseconds(Math.Min(dbDelay.TotalMilliseconds * 2, 15000));
                    }
                }
                _databaseInitialized = true;
            }

            var database = client.GetDatabase(dbName);
            var containerProperties = new ContainerProperties
            {
                Id = containerName,
                PartitionKeyPath = partitionKeyPath,
                DefaultTimeToLive = -1,
                IndexingPolicy = new IndexingPolicy
                {
                    Automatic = true,
                    IndexingMode = IndexingMode.Consistent,
                    IncludedPaths =
                    {
                        new IncludedPath { Path = "/type/?" },
                        new IncludedPath { Path = "/customerId/?" },
                        new IncludedPath { Path = "/status/?" }
                    },
                    ExcludedPaths = { new ExcludedPath { Path = "/*" } }
                }
            };

            var contDelay = TimeSpan.FromSeconds(1);
            for (var attempt = 1; attempt <= 20; attempt++)
            {
                try
                {
                    await database.CreateContainerIfNotExistsAsync(containerProperties);
                    break;
                }
                catch (CosmosException ex) when (IsTransient(ex) && attempt < 20)
                {
                    await Task.Delay(contDelay);
                    contDelay = TimeSpan.FromMilliseconds(Math.Min(contDelay.TotalMilliseconds * 2, 15000));
                }
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static bool IsTransient(CosmosException ex) =>
        ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
        || ex.StatusCode == System.Net.HttpStatusCode.RequestTimeout
        || ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests
        || ex.StatusCode == System.Net.HttpStatusCode.InternalServerError;
}
