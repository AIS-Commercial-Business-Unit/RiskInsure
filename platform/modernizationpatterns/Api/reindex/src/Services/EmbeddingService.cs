namespace RiskInsure.Modernization.Reindex.Services;

using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public interface IEmbeddingService
{
    Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default);

    Task<List<float[]>> EmbedBatchAsync(
        List<string> texts,
        CancellationToken cancellationToken = default);
}

public class EmbeddingService : IEmbeddingService
{
    private static readonly HttpClient HttpClient = new();
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _deploymentName;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(IConfiguration config, ILogger<EmbeddingService> logger)
    {
        _logger = logger;

        _endpoint = config["AzureOpenAI:Endpoint"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");

        _apiKey = config["AzureOpenAI:ApiKey"]?.Trim()
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");

        _deploymentName = config["AzureOpenAI:EmbeddingDeploymentName"] ?? "text-embedding-3-small";

        _logger.LogInformation("EmbeddingService initialized with deployment: {Deployment}", _deploymentName);
    }

    public async Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default)
    {
        var result = await EmbedBatchAsync(new List<string> { text }, cancellationToken);
        return result[0];
    }

    public async Task<List<float[]>> EmbedBatchAsync(
        List<string> texts,
        CancellationToken cancellationToken = default)
    {
        var allEmbeddings = new List<float[]>();

        const int batchSize = 8; // safer than 16
        const int maxRetries = 5;

        _logger.LogInformation("Embedding {Count} texts in batches of {BatchSize}", texts.Count, batchSize);

        for (int i = 0; i < texts.Count; i += batchSize)
        {
            var batch = texts.Skip(i).Take(batchSize).ToList();

            int attempt = 0;
            int delayMs = 2000;

            while (true)
            {
                attempt++;

                try
                {
                    var url = $"{_endpoint}/openai/deployments/{_deploymentName}/embeddings?api-version=2024-06-01";

                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Headers.Add("api-key", _apiKey);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var payload = JsonSerializer.Serialize(new { input = batch });
                    request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                    using var response = await HttpClient.SendAsync(request, cancellationToken);
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        using var document = JsonDocument.Parse(content);
                        var data = document.RootElement.GetProperty("data");

                        foreach (var item in data.EnumerateArray())
                        {
                            var embeddingArray = item.GetProperty("embedding");
                            var vector = new float[embeddingArray.GetArrayLength()];
                            int index = 0;

                            foreach (var value in embeddingArray.EnumerateArray())
                            {
                                vector[index++] = value.GetSingle();
                            }

                            allEmbeddings.Add(vector);
                        }

                        _logger.LogInformation(
                            "Batch {BatchNumber}: Embedded {Count} texts",
                            (i / batchSize) + 1,
                            batch.Count);

                        break; // success
                    }

                    // Handle rate limit (429)
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        int waitTime = delayMs;

                        // Respect Retry-After header if present
                        if (response.Headers.TryGetValues("Retry-After", out var values))
                        {
                            if (int.TryParse(values.First(), out var retryAfterSeconds))
                            {
                                waitTime = retryAfterSeconds * 1000;
                            }
                        }

                        _logger.LogWarning(
                            "Rate limit hit (429). Attempt {Attempt}/{MaxRetries}. Waiting {WaitTime}ms...",
                            attempt, maxRetries, waitTime);

                        if (attempt >= maxRetries)
                        {
                            throw new Exception("Max retries reached for embedding batch.");
                        }

                        await Task.Delay(waitTime, cancellationToken);
                        delayMs *= 2; // exponential backoff
                        continue;
                    }

                    // Other errors
                    throw new InvalidOperationException(
                        $"Embedding request failed: {(int)response.StatusCode} {response.ReasonPhrase}. Response: {content}");
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex,
                        "Error embedding batch (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms...",
                        attempt, maxRetries, delayMs);

                    await Task.Delay(delayMs, cancellationToken);
                    delayMs *= 2;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to embed batch at offset {Offset}", i);
                    throw;
                }
            }

            // Smooth traffic between batches
            if (i + batchSize < texts.Count)
            {
                await Task.Delay(1000, cancellationToken); // 1 sec gap
            }
        }

        _logger.LogInformation("Completed embedding {Total} texts", texts.Count);
        return allEmbeddings;
    }
}
