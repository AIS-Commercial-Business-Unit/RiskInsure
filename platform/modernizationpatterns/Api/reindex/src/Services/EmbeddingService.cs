namespace RiskInsure.Modernization.Reindex.Services;

using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

/// <summary>
/// Wraps Azure OpenAI for generating embeddings in the Reindex Service.
///
/// Only needs embedding capability (no chat completions here).
/// The Chat API has its own OpenAiService — this one is separate
/// because each service should be independently deployable.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Convert text into a vector (list of 1536 numbers) that captures its meaning.
    /// Similar texts will have similar vectors, enabling semantic search.
    /// </summary>
    Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Embed multiple texts in one API call (more efficient than one-by-one).
    /// </summary>
    Task<List<float[]>> EmbedBatchAsync(List<string> texts, CancellationToken cancellationToken = default);
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

        var endpoint = config["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
        var apiKey = config["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");
        _endpoint = endpoint.TrimEnd('/');
        _apiKey = apiKey.Trim();
        _deploymentName = config["AzureOpenAI:EmbeddingDeploymentName"] ?? "text-embedding-3-small";

        _logger.LogInformation("EmbeddingService initialized with deployment: {Deployment}", _deploymentName);
    }

    public async Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var embeddings = await EmbedBatchAsync(new List<string> { text }, cancellationToken);
            var embedding = embeddings[0];

            _logger.LogDebug("Embedded text ({Length} chars) → {Dimensions} dimensions",
                text.Length, embedding.Length);

            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to embed text");
            throw;
        }
    }

    /// <summary>
    /// Embeds multiple texts in batches of 16 (Azure OpenAI limit per request).
    /// More efficient than calling EmbedTextAsync in a loop.
    /// </summary>
    public async Task<List<float[]>> EmbedBatchAsync(
        List<string> texts,
        CancellationToken cancellationToken = default)
    {
        var allEmbeddings = new List<float[]>();
        const int batchSize = 8; // Reduced from 16 to 8 to lower per-request load (Free tier friendly)
        const int maxRetries = 5;

        _logger.LogInformation(
            "Embedding {Count} texts in batches of {BatchSize} (may take several minutes due to Free tier rate limits)",
            texts.Count, batchSize);

        for (int i = 0; i < texts.Count; i += batchSize)
        {
            var batch = texts.Skip(i).Take(batchSize).ToList();
            var batchNum = (i / batchSize) + 1;
            var totalBatches = (int)Math.Ceiling((double)texts.Count / batchSize);
            var retryCount = 0;
            bool success = false;

            while (retryCount < maxRetries && !success)
            {
                try
                {
                    var url = $"{_endpoint}/openai/deployments/{_deploymentName}/embeddings?api-version=2024-06-01";
                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Headers.Add("api-key", _apiKey);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var payload = JsonSerializer.Serialize(new
                    {
                        input = batch
                    });

                    request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                    using var response = await HttpClient.SendAsync(request, cancellationToken);
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);

                    // Handle rate limiting (429) with exponential backoff
                    if ((int)response.StatusCode == 429)
                    {
                        var delaySeconds = (int)Math.Pow(2, retryCount) * 10; // 10s, 20s, 40s, 80s, 160s
                        _logger.LogWarning(
                            "Rate limit hit (429) on batch {BatchNum}/{TotalBatches}, waiting {Delay}s before retry {Retry}/{MaxRetries}",
                            batchNum, totalBatches, delaySeconds, retryCount + 1, maxRetries);
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                        retryCount++;
                        continue; // Retry this batch
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException(
                            $"Embedding request failed: {(int)response.StatusCode} {response.ReasonPhrase}. Response: {content}");
                    }

                    using var document = JsonDocument.Parse(content);
                    var data = document.RootElement.GetProperty("data");

                    foreach (var item in data.EnumerateArray())
                    {
                        var embeddingArray = item.GetProperty("embedding");
                        var vector = new float[embeddingArray.GetArrayLength()];
                        var index = 0;

                        foreach (var value in embeddingArray.EnumerateArray())
                        {
                            vector[index++] = value.GetSingle();
                        }

                        allEmbeddings.Add(vector);
                    }

                    _logger.LogInformation(
                        "Batch {BatchNum}/{TotalBatches}: Embedded {Count} texts successfully",
                        batchNum, totalBatches, batch.Count);

                    success = true;

                    // Aggressive delay between batches to stay under Free tier rate limits (~3-6 RPM)
                    // Batch size 8 + 5s delay = ~12 requests per minute (safe margin below 6 RPM)
                    if (i + batchSize < texts.Count)
                    {
                        _logger.LogDebug("Waiting 5s before next batch to respect rate limits...");
                        await Task.Delay(5000, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // Don't retry cancellations
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError(ex,
                            "Failed to embed batch {BatchNum}/{TotalBatches} after {MaxRetries} retries at offset {Offset}",
                            batchNum, totalBatches, maxRetries, i);
                        throw;
                    }

                    var delaySeconds = (int)Math.Pow(2, retryCount) * 5;
                    _logger.LogWarning(ex,
                        "Failed to embed batch {BatchNum}/{TotalBatches}, waiting {Delay}s before retry {Retry}/{MaxRetries}",
                        batchNum, totalBatches, delaySeconds, retryCount, maxRetries);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
            }

            if (!success)
            {
                throw new InvalidOperationException(
                    $"Failed to embed batch {batchNum}/{totalBatches} after {maxRetries} retries");
            }
        }

        return allEmbeddings;
    }
}
