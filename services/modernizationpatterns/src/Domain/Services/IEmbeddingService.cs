namespace RiskInsure.ModernizationPatternsMgt.Domain.Services;

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
