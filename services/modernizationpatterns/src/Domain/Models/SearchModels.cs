namespace RiskInsure.ModernizationPatternsMgt.Domain.Models;

/// <summary>
/// Result item from Azure AI Search
/// </summary>
public class SearchResultItem
{
    public required string Id { get; init; }
    public required string PatternSlug { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string Content { get; init; }
    public required double Relevance { get; init; }
}

/// <summary>
/// Represents one chunk of a pattern, ready to be embedded and indexed.
/// Think of it as one "page" from a "book" (the full pattern),
/// small enough for the AI to process efficiently.
/// </summary>
public class PatternChunk
{
    /// <summary>Unique ID for this chunk (e.g., "strangler-fig-migration_overview")</summary>
    public required string Id { get; init; }

    /// <summary>Which pattern this chunk belongs to</summary>
    public required string PatternSlug { get; init; }

    /// <summary>Pattern title for display</summary>
    public required string Title { get; init; }

    /// <summary>Pattern category for filtering</summary>
    public required string Category { get; init; }

    /// <summary>What kind of chunk: overview, implementation, complexity, guidance</summary>
    public required string ChunkType { get; init; }

    /// <summary>The actual text content that will be embedded and searched</summary>
    public required string Content { get; init; }

    /// <summary>Order within the pattern</summary>
    public required int ChunkIndex { get; init; }
}

/// <summary>
/// A document ready to be uploaded to the search index.
/// Contains both the text content AND its vector embedding.
/// </summary>
public class IndexDocument
{
    public required string Id { get; init; }
    public required string PatternSlug { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string ChunkType { get; init; }
    public required int ChunkIndex { get; init; }
    public required string Content { get; init; }

    /// <summary>
    /// The vector embedding — a list of 1536 floating-point numbers
    /// that capture the "meaning" of the content.
    /// </summary>
    public required float[] ContentVector { get; init; }
}
