using Microsoft.Azure.Cosmos;

namespace RiskInsure.PolicyLifeCycleMgt.Infrastructure;

public static class CosmosDbInitializer
{
    public static async Task EnsureDbAndContainerAsync(
        CosmosClient client,
        string dbName,
        string containerName,
        string partitionKeyPath,
        int? throughput = 400)
    {
        var delay = TimeSpan.FromSeconds(1);
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                var dbResponse = await client.CreateDatabaseIfNotExistsAsync(dbName, throughput: throughput);
                await dbResponse.Database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties
                    {
                        Id = containerName,
                        PartitionKeyPath = partitionKeyPath,
                        DefaultTimeToLive = -1
                    });
                return;
            }
            catch (CosmosException ex) when (IsTransient(ex) && attempt < 20)
            {
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 15000));
            }
        }
    }

    private static bool IsTransient(CosmosException ex) =>
        ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
        || ex.StatusCode == System.Net.HttpStatusCode.RequestTimeout
        || ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests
        || ex.StatusCode == System.Net.HttpStatusCode.InternalServerError;
}
