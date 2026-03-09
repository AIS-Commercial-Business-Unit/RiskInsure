namespace RiskInsure.Modernization.Chat.Services;

using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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

public class OpenAiService : IOpenAiService
{
    private static readonly HttpClient HttpClient = new();
    private readonly OpenAIClient _client;
    private readonly string _endpoint;
    private readonly string _apiKey;
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

        _endpoint = endpoint.TrimEnd('/');
        _apiKey = apiKey;

        _chatDeploymentName = config["AzureOpenAI:ChatDeploymentName"] ?? "gpt-4.1";
        _embeddingDeploymentName = config["AzureOpenAI:EmbeddingDeploymentName"] ?? "text-embedding-3-small";

        _client = new OpenAIClient(new Uri(_endpoint), new Azure.AzureKeyCredential(_apiKey));
    }

    public async Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty", nameof(text));

        try
        {
            _logger.LogDebug("Embedding text: {TextLength} chars", text.Length);

            var url = $"{_endpoint}/openai/deployments/{_embeddingDeploymentName}/embeddings?api-version=2024-06-01";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("api-key", _apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = JsonSerializer.Serialize(new
            {
                input = text
            });

            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Embedding request failed: {(int)response.StatusCode} {response.ReasonPhrase}. Response: {content}");
            }

            using var document = JsonDocument.Parse(content);
            var embeddingArray = document.RootElement
                .GetProperty("data")[0]
                .GetProperty("embedding");

            var result = new float[embeddingArray.GetArrayLength()];
            var index = 0;
            foreach (var value in embeddingArray.EnumerateArray())
            {
                result[index++] = value.GetSingle();
            }

            return result;
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

            // Build chat messages for Chat Completions API (works with gpt-4.1)
            var chatOptions = new ChatCompletionsOptions
            {
                DeploymentName = _chatDeploymentName,
                MaxTokens = 800,
                Temperature = 0.2f
            };

            // System prompt with RAG context
            chatOptions.Messages.Add(new ChatRequestSystemMessage(systemPrompt));

            // Add conversation history (last 6 messages)
            if (history != null && history.Count > 0)
            {
                foreach (var msg in history.TakeLast(6))
                {
                    if (msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                    {
                        chatOptions.Messages.Add(new ChatRequestAssistantMessage(msg.Content));
                    }
                    else
                    {
                        chatOptions.Messages.Add(new ChatRequestUserMessage(msg.Content));
                    }
                }
            }

            // Current user message
            chatOptions.Messages.Add(new ChatRequestUserMessage(userMessage));

            try
            {
                var response = await _client.GetChatCompletionsAsync(chatOptions, cancellationToken);
                var choice = response.Value.Choices?.FirstOrDefault();
                var content = choice?.Message?.Content ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogInformation("Chat completion from Azure: {Length} chars", content.Length);
                    return content;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Azure Chat Completions API call failed, using fallback");
            }

            // Fallback to local generation if Azure fails
            return GenerateFallback(userMessage, history ?? new List<ConversationMessage>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetCompletionAsync");
            return GenerateFallback(userMessage, history ?? new List<ConversationMessage>());
        }
    }

    private string GenerateFallback(string userMessage, List<ConversationMessage> history)
    {
        var contextInfo = history.Count > 0
            ? $"Based on our conversation history, and considering your question about '{userMessage}'"
            : $"Regarding your question about '{userMessage}'";

        return $@"{contextInfo}, here are some best practices for modernization patterns:

1. Event-Driven Architecture: Decouple domains using events.
2. Strangler Fig Migration: Gradually replace legacy systems.
3. API Composition: Aggregate data from multiple services.
4. Domain-Event Contracts: Use versioned contracts.
5. Observability First: Instrument systems from day one.
";
    }
}

public record ConversationMessage(string Role, string Content, DateTimeOffset Timestamp);
