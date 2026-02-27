namespace RiskInsure.Modernization.Reindex.Services;

using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;

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
    private readonly OpenAIClient _client;
    private readonly string _deploymentName;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(IConfiguration config, ILogger<EmbeddingService> logger)
    {
        _logger = logger;

        var endpoint = config["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
        var apiKey = config["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");
        _deploymentName = config["AzureOpenAI:EmbeddingDeploymentName"] ?? "text-embedding-3-small";

        _client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _logger.LogInformation("EmbeddingService initialized with deployment: {Deployment}", _deploymentName);
    }

    public async Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new EmbeddingsOptions(_deploymentName, new[] { text });
            var response = await _client.GetEmbeddingsAsync(options, cancellationToken);
            var embedding = response.Value.Data[0].Embedding.ToArray();

            _logger.LogDebug("Embedded text ({Length} chars) → {Dimensions} dimensions",
                text.Length, embedding.Length);

            return embedding;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to embed text: {ErrorCode}", ex.ErrorCode);
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
        const int batchSize = 16; // Azure OpenAI batch limit

        _logger.LogInformation("Embedding {Count} texts in batches of {BatchSize}", texts.Count, batchSize);

        for (int i = 0; i < texts.Count; i += batchSize)
        {
            var batch = texts.Skip(i).Take(batchSize).ToList();

            try
            {
                var options = new EmbeddingsOptions(_deploymentName, batch);
                var response = await _client.GetEmbeddingsAsync(options, cancellationToken);

                foreach (var item in response.Value.Data)
                {
                    allEmbeddings.Add(item.Embedding.ToArray());
                }

                _logger.LogInformation(
                    "Batch {BatchNum}/{TotalBatches}: Embedded {Count} texts",
                    (i / batchSize) + 1,
                    (int)Math.Ceiling((double)texts.Count / batchSize),
                    batch.Count);

                // Small delay between batches to avoid rate limiting
                if (i + batchSize < texts.Count)
                {
                    await Task.Delay(200, cancellationToken);
                }
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex,
                    "Failed to embed batch at offset {Offset}: {ErrorCode}",
                    i, ex.ErrorCode);
                throw;
            }
        }

        return allEmbeddings;
    }
}
