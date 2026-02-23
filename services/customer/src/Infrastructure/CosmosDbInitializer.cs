namespace RiskInsure.Customer.Infrastructure;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Net;

public class CosmosDbInitializer
{
    private readonly CosmosClient _cosmosClient;
    private readonly ILogger<CosmosDbInitializer> _logger;
    private const string DatabaseName = "RiskInsure";
    private const string ContainerName = "customer";
    private const int MaxInitializationAttempts = 12;
    private const int InitialRetryDelayMilliseconds = 1000;

    public CosmosDbInitializer(CosmosClient cosmosClient, ILogger<CosmosDbInitializer> logger)
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Container> InitializeAsync()
    {
        var retryDelay = TimeSpan.FromMilliseconds(InitialRetryDelayMilliseconds);

        for (var attempt = 1; attempt <= MaxInitializationAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Initializing Cosmos DB database {DatabaseName} (attempt {Attempt}/{MaxAttempts})",
                    DatabaseName,
                    attempt,
                    MaxInitializationAttempts);

                var database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseName);

                _logger.LogInformation(
                    "Creating container {ContainerName} with partition key /customerId (attempt {Attempt}/{MaxAttempts})",
                    ContainerName,
                    attempt,
                    MaxInitializationAttempts);

                var containerResponse = await database.Database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties
                    {
                        Id = ContainerName,
                        PartitionKeyPath = "/customerId"
                    });

                _logger.LogInformation("Cosmos DB initialization complete");
                return containerResponse.Container;
            }
            catch (CosmosException ex) when (IsTransient(ex) && attempt < MaxInitializationAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Transient Cosmos initialization failure with status {StatusCode} and substatus {SubStatusCode}. Retrying in {RetryDelayMs}ms (attempt {Attempt}/{MaxAttempts})",
                    ex.StatusCode,
                    ex.SubStatusCode,
                    (int)retryDelay.TotalMilliseconds,
                    attempt,
                    MaxInitializationAttempts);

                await Task.Delay(retryDelay);
                retryDelay = TimeSpan.FromMilliseconds(Math.Min(retryDelay.TotalMilliseconds * 2, 15000));
            }
        }

        throw new InvalidOperationException("Failed to initialize Cosmos DB container after multiple attempts.");
    }

    private static bool IsTransient(CosmosException exception)
    {
        return exception.StatusCode == HttpStatusCode.ServiceUnavailable
            || exception.StatusCode == HttpStatusCode.RequestTimeout
            || exception.StatusCode == HttpStatusCode.TooManyRequests
            || exception.StatusCode == HttpStatusCode.InternalServerError
            || exception.StatusCode == HttpStatusCode.BadGateway
            || exception.StatusCode == HttpStatusCode.GatewayTimeout;
    }
}
