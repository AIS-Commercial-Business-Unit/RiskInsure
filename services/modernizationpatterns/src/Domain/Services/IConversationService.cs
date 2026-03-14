namespace RiskInsure.ModernizationPatternsMgt.Domain.Services;

using RiskInsure.ModernizationPatternsMgt.Domain.Models;

/// <summary>
/// Cosmos DB conversation persistence service
/// </summary>
public interface IConversationService
{
    Task<List<Conversation>> GetUserConversationsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<Conversation?> GetConversationAsync(
        string conversationId,
        string userId,
        CancellationToken cancellationToken = default);

    Task SaveConversationAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default);
}
