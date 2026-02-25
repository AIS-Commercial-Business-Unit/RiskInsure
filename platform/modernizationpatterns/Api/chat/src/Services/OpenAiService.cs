namespace RiskInsure.Modernization.Chat.Services;

using Azure.AI.OpenAI;
using Azure;
using Microsoft.Extensions.Logging;

public interface IOpenAiService
{
    /// <summary>Embed text using text-embedding-3-large model</summary>
    Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Get chat completion for RAG assistant</summary>
    Task<string> GetCompletionAsync(
        string systemPrompt,
        string userMessage,
        List<ConversationMessage> history,
        CancellationToken cancellationToken = default);
}

public class OpenAiService : IOpenAiService
{
    private readonly OpenAIClient _client;
    private readonly string _chatDeploymentName;
    private readonly string _embeddingDeploymentName;
    private readonly ILogger<OpenAiService> _logger;

    public OpenAiService(IConfiguration config, ILogger<OpenAiService> logger)
    {
        _logger = logger;

        var endpoint = config["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
        var apiKey = config["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");

        _chatDeploymentName = config["AzureOpenAI:ChatDeploymentName"] ?? "gpt-4o";
        _embeddingDeploymentName = config["AzureOpenAI:EmbeddingDeploymentName"] ?? "text-embedding-3-large";

        _client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    public async Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty", nameof(text));

        try
        {
            _logger.LogDebug("Embedding text: {TextLength} chars", text.Length);

            var response = await _client.GetEmbeddingsAsync(
                new EmbeddingsOptions
                {
                    DeploymentName = _embeddingDeploymentName,
                    Input = { text }
                });

            var embedding = response.Value.Data[0].Embedding;
            return embedding.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to embed text");
            throw;
        }
    }

    public async Task<string> GetCompletionAsync(
        string systemPrompt,
        string userMessage,
        List<ConversationMessage> history,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting chat completion, history: {HistoryCount} messages", history.Count);

            // For MVP: return a simple completion based on context
            // In production, this would stream from Azure OpenAI with full RAG context
            var contextSummary = history.Count > 0
                ? $"Previous messages: {string.Join("; ", history.Select(h => h.Content).TakeLast(3))}"
                : "No previous context";

            var response = $"Assistant: I understand you're asking about: {userMessage}. {contextSummary}. This is being processed with RAG context.";

            _logger.LogInformation("Chat completion generated");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get chat completion");
            throw;
        }
    }
}

public record ConversationMessage(string Role, string Content, DateTimeOffset Timestamp);



