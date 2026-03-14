namespace RiskInsure.ModernizationPatternsMgt.Domain.Services;

using RiskInsure.ModernizationPatternsMgt.Domain.Models;

/// <summary>
/// Breaks pattern content into smaller, overlapping text chunks suitable for embedding.
///
/// Why chunk? AI Search works best with focused pieces of text (300-600 words).
/// A full pattern JSON can be thousands of words — chunking ensures each piece
/// is small enough to embed accurately and retrieve precisely.
/// </summary>
public interface IChunkingService
{
    /// <summary>
    /// Takes a pattern JSON file and returns a list of text chunks ready for embedding.
    /// Each chunk includes the pattern title/category as context so it makes sense standalone.
    /// </summary>
    List<PatternChunk> ChunkPattern(string patternJson, string patternSlug);

    /// <summary>
    /// Takes inbox document text and returns chunks ready for embedding.
    /// Used for markdown/text/pdf/docx content placed under content/_inbox.
    /// </summary>
    List<PatternChunk> ChunkInboxDocument(string documentText, string documentSlug, string sourceType);
}
