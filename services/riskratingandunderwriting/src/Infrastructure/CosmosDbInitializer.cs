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
        var delay = TimeSpan.FromSeconds(1);
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
                await database.Database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties { Id = containerName, PartitionKeyPath = partitionKeyPath });
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
