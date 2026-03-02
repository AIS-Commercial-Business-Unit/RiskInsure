namespace RiskInsure.Modernization.Chat.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.Modernization.Chat.Models;

/// <summary>
/// Custom Cosmos serializer using System.Text.Json (required because models use [JsonPropertyName] attributes)
/// </summary>
public class CosmosSystemTextJsonSerializer : CosmosSerializer
{
    private readonly JsonSerializerOptions _options;

    public CosmosSystemTextJsonSerializer()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }

    public override T FromStream<T>(Stream stream)
    {
        if (stream.CanSeek && stream.Length == 0)
            return default!;

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<T>(json, _options)!;
    }

    public override Stream ToStream<T>(T input)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, input, _options);
        stream.Position = 0;
        return stream;
    }
}

public interface IConversationService
{
    Task<Conversation?> GetConversationAsync(
        string conversationId,
        string userId,
        CancellationToken cancellationToken = default);

    Task SaveConversationAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default);
}

public class ConversationService : IConversationService
{
    private readonly Container _container;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(IConfiguration config, ILogger<ConversationService> logger)
    {
        _logger = logger;

        var connectionString = config.GetConnectionString("CosmosDb")
            ?? throw new InvalidOperationException("CosmosDb connection string not configured");

        var databaseName = config["CosmosDb:DatabaseName"] ?? "modernization-patterns-db";
        const string containerName = "conversations";

        CosmosClient client;
        try
        {
            // Custom serializer for System.Text.Json compatibility
            var serializer = new CosmosSystemTextJsonSerializer();

            // If using local emulator, configure SSL bypass
            if (connectionString.Contains("localhost:8081", StringComparison.OrdinalIgnoreCase))
            {
                var clientOptions = new CosmosClientOptions
                {
                    HttpClientFactory = () =>
                    {
                        var handler = new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                        };
                        return new HttpClient(handler);
                    },
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = serializer
                };

                client = new CosmosClient(connectionString, clientOptions);
                _logger.LogInformation("Using Cosmos DB emulator with SSL validation bypass and System.Text.Json serializer");
            }
            else
            {
                var clientOptions = new CosmosClientOptions
                {
                    Serializer = serializer
                };
                client = new CosmosClient(connectionString, clientOptions);
                _logger.LogInformation("Using Cosmos DB service with System.Text.Json serializer");
            }

            // Ensure database and container exist (idempotent)
            try
            {
                _logger.LogInformation("Attempting to create/verify database {DatabaseName}", databaseName);

                // Wrap with timeout to prevent blocking (increased to 15s for Azure)
                var createDbTask = client.CreateDatabaseIfNotExistsAsync(databaseName);
                if (!createDbTask.Wait(TimeSpan.FromSeconds(15)))
                {
                    throw new TimeoutException("Cosmos DB database creation timed out after 15 seconds");
                }

                var dbResponse = createDbTask.Result;
                var database = dbResponse.Database;

                _logger.LogInformation("Database {DatabaseName} verified/created", databaseName);
                _logger.LogInformation("Attempting to create/verify container {ContainerName}", containerName);

                var createContainerTask = database.CreateContainerIfNotExistsAsync(new ContainerProperties
                {
                    Id = containerName,
                    PartitionKeyPath = "/userId"
                });

                if (!createContainerTask.Wait(TimeSpan.FromSeconds(15)))
                {
                    throw new TimeoutException("Cosmos DB container creation timed out after 15 seconds");
                }

                createContainerTask.Wait();
                _container = database.GetContainer(containerName);
                _logger.LogInformation("Container {ContainerName} verified/created", containerName);
            }
            catch (TimeoutException timeoutEx)
            {
                _logger.LogWarning(timeoutEx, "Cosmos DB initialization timed out; will use fallback mode without persistence");
                _container = null!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Cosmos DB client/database/container. Conversations will be unavailable locally.");
            // Create a fallback in-memory placeholder container reference will be null
            _container = null!; // allow null and handle in methods
        }
    }

    public async Task<Conversation?> GetConversationAsync(
        string conversationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving conversation {ConversationId} for user {UserId}",
            conversationId, userId);

        try
        {
            if (_container == null)
            {
                _logger.LogWarning("Cosmos container not initialized; returning null conversation");
                return null;
            }

            var response = await _container.ReadItemAsync<Conversation>(
                conversationId,
                new PartitionKey(userId),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Conversation not found: {ConversationId}", conversationId);
            return null;
        }
    }

    public async Task SaveConversationAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Saving conversation {ConversationId} for user {UserId}",
                conversation.Id, conversation.UserId);

            if (_container == null)
            {
                _logger.LogWarning("Cosmos container not initialized; skipping persistence for conversation {ConversationId}", conversation.Id);
                return;
            }

            await _container.UpsertItemAsync(conversation, new PartitionKey(conversation.UserId), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to save conversation {ConversationId}", conversation.Id);
            throw;
        }
    }
}
