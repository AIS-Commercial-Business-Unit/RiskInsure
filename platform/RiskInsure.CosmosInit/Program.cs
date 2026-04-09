using Microsoft.Azure.Cosmos;

return await CosmosInitializer.RunAsync();

static class CosmosInitializer
{
    private const string DatabaseName = "RiskInsure";
    private const int DatabaseThroughputRus = 1000;
    private const int MaxAttempts = 40;

    // All containers provisioned once before any domain service starts.
    // Sequential creation avoids the simultaneous-create burst that triggers
    // Cosmos Emulator 503 / substatus 1007 throttling.
    private static readonly (string Name, string PartitionKeyPath)[] Containers =
    [
        // ── Domain data containers ────────────────────────────────────────
        ("customerrelationships",             "/customerId"),
        ("fundtransfermgt-paymentmethods",    "/customerId"),
        ("fundtransfermgt-transactions",      "/customerId"),
        ("PolicyEquityAndInvoicingMgt",       "/accountId"),
        ("policylifecycle",                   "/policyId"),
        ("riskratingandunderwriting",         "/quoteId"),
        ("file-processing-configurations",    "/clientId"),
        ("file-processing-executions",        "/clientId"),
        ("file-processing-discovered-files",  "/clientId"),
        ("file-processing-processed-files",   "/clientId"),

        // ── NServiceBus saga containers ───────────────────────────────────
        ("customerrelationshipsmgt-sagas",    "/customerId"),
        ("fundstransfermgt-sagas",            "/transactionId"),
        ("policyequityandinvoicingmgt-sagas", "/accountId"),
        ("policylifecyclemgt-sagas",          "/policyId"),
        ("riskratingandunderwriting-sagas",   "/quoteId"),
    ];

    public static async Task<int> RunAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CosmosDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__CosmosDb environment variable is required.");

        Console.WriteLine(
            $"[cosmos-init] Starting Cosmos DB initialization for database '{DatabaseName}'");

        var clientOptions = new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            RequestTimeout = TimeSpan.FromSeconds(60),
            MaxRetryAttemptsOnRateLimitedRequests = 9,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(60),
        };

        using var client = new CosmosClient(connectionString, clientOptions);

        // Step 1: Create shared database
        Console.WriteLine(
            $"[cosmos-init] Creating database '{DatabaseName}' " +
            $"with {DatabaseThroughputRus} RU/s shared throughput...");

        await CreateDatabaseAsync(client, DatabaseName, DatabaseThroughputRus);
        Console.WriteLine($"[cosmos-init] Database '{DatabaseName}' ready.");

        // Step 2: Create every container in sequence — avoids parallel-create
        //         storm that causes 503/1007 on the local emulator.
        var database = client.GetDatabase(DatabaseName);
        foreach (var (name, partitionKeyPath) in Containers)
        {
            Console.WriteLine(
                $"[cosmos-init] Creating container '{name}' " +
                $"(partitionKey: {partitionKeyPath})...");

            await CreateContainerAsync(database, name, partitionKeyPath);
            Console.WriteLine($"[cosmos-init] Container '{name}' ready.");
        }

        Console.WriteLine("[cosmos-init] All Cosmos DB resources initialized successfully.");
        return 0;
    }

    private static async Task CreateDatabaseAsync(
        CosmosClient client, string dbName, int throughputRus)
    {
        var delay = TimeSpan.FromSeconds(1);
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await client.CreateDatabaseIfNotExistsAsync(
                    dbName,
                    ThroughputProperties.CreateManualThroughput(throughputRus));
                return;
            }
            catch (CosmosException ex) when (IsTransient(ex) && attempt < MaxAttempts)
            {
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(200, 800));
                Console.WriteLine(
                    $"[cosmos-init] Database '{dbName}' attempt {attempt}/{MaxAttempts} " +
                    $"failed ({(int)ex.StatusCode}/{ex.SubStatusCode}), " +
                    $"retrying in {(delay + jitter).TotalSeconds:F1}s...");
                await Task.Delay(delay + jitter);
                delay = TimeSpan.FromMilliseconds(
                    Math.Min(delay.TotalMilliseconds * 2, 20000));
            }
        }

        throw new InvalidOperationException(
            $"Failed to create database '{dbName}' after {MaxAttempts} attempts.");
    }

    private static async Task CreateContainerAsync(
        Database database, string containerName, string partitionKeyPath)
    {
        var delay = TimeSpan.FromSeconds(1);
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await database.CreateContainerIfNotExistsAsync(new ContainerProperties
                {
                    Id = containerName,
                    PartitionKeyPath = partitionKeyPath,
                    DefaultTimeToLive = -1,
                });
                return;
            }
            catch (CosmosException ex) when (IsTransient(ex) && attempt < MaxAttempts)
            {
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(200, 800));
                Console.WriteLine(
                    $"[cosmos-init] Container '{containerName}' attempt {attempt}/{MaxAttempts} " +
                    $"failed ({(int)ex.StatusCode}/{ex.SubStatusCode}), " +
                    $"retrying in {(delay + jitter).TotalSeconds:F1}s...");
                await Task.Delay(delay + jitter);
                delay = TimeSpan.FromMilliseconds(
                    Math.Min(delay.TotalMilliseconds * 2, 20000));
            }
        }

        throw new InvalidOperationException(
            $"Failed to create container '{containerName}' after {MaxAttempts} attempts.");
    }

    private static bool IsTransient(CosmosException ex) =>
        ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
        || ex.StatusCode == System.Net.HttpStatusCode.RequestTimeout
        || ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests
        || ex.StatusCode == System.Net.HttpStatusCode.InternalServerError;
}
