namespace RiskInsure.Modernization.Chat.Services;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.Modernization.Chat.Models;

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

        var client = new CosmosClient(connectionString);
        var database = client.GetDatabase(databaseName);
        _container = database.GetContainer(containerName);
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

            await _container.UpsertItemAsync(conversation, cancellationToken: cancellationToken);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to save conversation {ConversationId}", conversation.Id);
            throw;
        }
    }
}
