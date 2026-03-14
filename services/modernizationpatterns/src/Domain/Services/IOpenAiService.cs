namespace RiskInsure.ModernizationPatternsMgt.Domain.Services;

using RiskInsure.ModernizationPatternsMgt.Domain.Models;

/// <summary>
/// Azure OpenAI service for embeddings and chat completions
/// </summary>
public interface IOpenAiService
{
    /// <summary>Embed text using text-embedding-3-small model</summary>
    Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Get chat completion using gpt-4.1 (Chat Completions API)</summary>
    Task<string> GetCompletionAsync(
        string systemPrompt,
        string userMessage,
        List<ConversationMessage> history,
        CancellationToken cancellationToken = default);
}
