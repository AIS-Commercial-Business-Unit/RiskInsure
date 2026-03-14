namespace RiskInsure.ModernizationPatternsMgt.Domain.Services;

using RiskInsure.ModernizationPatternsMgt.Domain.Models;

/// <summary>
/// Manages the Azure AI Search index: creates the schema and uploads documents.
///
/// Think of this as the "library catalog system". It:
/// 1. Creates the shelf structure (index schema) — what fields exist
/// 2. Puts books on shelves (uploads chunked documents with their embeddings)
///
/// The index stores both the text AND the vector (numbers) so the Chat API
/// can search by meaning, not just keywords.
/// </summary>
public interface IIndexingService
{
    /// <summary>Create or update the AI Search index schema</summary>
    Task<bool> CreateOrUpdateIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>Upload a batch of chunks (with embeddings) to the search index</summary>
    Task<int> UploadDocumentsAsync(
        List<IndexDocument> documents,
        CancellationToken cancellationToken = default);

    /// <summary>Delete all documents from the index (for clean re-indexing)</summary>
    Task<int> DeleteAllDocumentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Get the count of documents currently in the index</summary>
    Task<long> GetDocumentCountAsync(CancellationToken cancellationToken = default);
}
