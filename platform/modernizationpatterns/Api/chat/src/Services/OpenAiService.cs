namespace RiskInsure.Modernization.Chat.Services;

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
        _apiKey = apiKey.Trim();

        _chatDeploymentName = config["AzureOpenAI:ChatDeploymentName"] ?? "gpt-4.1";
        _embeddingDeploymentName = config["AzureOpenAI:EmbeddingDeploymentName"] ?? "text-embedding-3-small";
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
        if (string.IsNullOrWhiteSpace(userMessage))
            throw new ArgumentException("User message cannot be empty", nameof(userMessage));

        try
        {
            var safeHistory = history ?? new List<ConversationMessage>();
            _logger.LogInformation("Getting chat completion, history: {HistoryCount} messages", safeHistory.Count);

            var messages = new List<Dictionary<string, string>>
            {
                new()
                {
                    ["role"] = "system",
                    ["content"] = systemPrompt
                }
            };

            foreach (var msg in safeHistory.TakeLast(6))
            {
                messages.Add(new Dictionary<string, string>
                {
                    ["role"] = msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user",
                    ["content"] = msg.Content
                });
            }

            messages.Add(new Dictionary<string, string>
            {
                ["role"] = "user",
                ["content"] = userMessage
            });

            var url = $"{_endpoint}/openai/deployments/{_chatDeploymentName}/chat/completions?api-version=2024-06-01";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("api-key", _apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = JsonSerializer.Serialize(new
            {
                messages,
                temperature = 0.2,
                max_tokens = 800
            });

            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Chat completion request failed: {(int)response.StatusCode} {response.ReasonPhrase}. Response: {content}");
            }

            using var document = JsonDocument.Parse(content);
            var completion = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(completion))
            {
                throw new InvalidOperationException("Chat completion response did not include content");
            }

            _logger.LogInformation("Chat completion from Azure: {Length} chars", completion.Length);
            return completion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetCompletionAsync");
            throw;
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
