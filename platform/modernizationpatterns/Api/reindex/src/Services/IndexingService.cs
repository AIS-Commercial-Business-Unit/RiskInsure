namespace RiskInsure.Modernization.Reindex.Services;

using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;

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

public class IndexingService : IIndexingService
{
    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;
    private readonly ILogger<IndexingService> _logger;
    private readonly string _indexName;

    // Must match the embedding model's dimension (text-embedding-3-small = 1536 dimensions)
    private const int EmbeddingDimensions = 1536;

    public IndexingService(IConfiguration config, ILogger<IndexingService> logger)
    {
        _logger = logger;

        var endpoint = config["AzureSearch:Endpoint"]
            ?? throw new InvalidOperationException("AzureSearch:Endpoint not configured");
        var apiKey = config["AzureSearch:ApiKey"]
            ?? throw new InvalidOperationException("AzureSearch:ApiKey not configured");
        _indexName = config["AzureSearch:IndexName"] ?? "modernization-patterns";

        var credential = new AzureKeyCredential(apiKey);
        _indexClient = new SearchIndexClient(new Uri(endpoint), credential);
        _searchClient = new SearchClient(new Uri(endpoint), _indexName, credential);
    }

    /// <summary>
    /// Creates (or updates) the search index with the correct schema.
    ///
    /// The schema defines:
    /// - id: unique identifier for each chunk
    /// - patternSlug: which pattern this belongs to (for filtering)
    /// - title: pattern name (searchable + displayed)
    /// - category: for faceted filtering
    /// - chunkType: overview/implementation/complexity/guidance
    /// - content: the actual text (searchable)
    /// - contentVector: the embedding numbers (for semantic search)
    /// </summary>
    public async Task<bool> CreateOrUpdateIndexAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating or updating search index: {IndexName}", _indexName);

        try
        {
            var definition = new SearchIndex(_indexName)
            {
                Fields = new List<SearchField>
                {
                    // Key field — unique per chunk
                    new SimpleField("id", SearchFieldDataType.String)
                    {
                        IsKey = true,
                        IsFilterable = true
                    },

                    // Pattern identifier — for grouping/filtering
                    new SimpleField("patternSlug", SearchFieldDataType.String)
                    {
                        IsFilterable = true,
                        IsFacetable = true
                    },

                    // Title — searchable and retrievable
                    new SearchableField("title")
                    {
                        IsFilterable = true,
                        IsSortable = true
                    },

                    // Category — for filtering by category
                    new SimpleField("category", SearchFieldDataType.String)
                    {
                        IsFilterable = true,
                        IsFacetable = true
                    },

                    // Chunk type — overview, implementation, complexity, guidance
                    new SimpleField("chunkType", SearchFieldDataType.String)
                    {
                        IsFilterable = true
                    },

                    // Chunk index — ordering within a pattern
                    new SimpleField("chunkIndex", SearchFieldDataType.Int32)
                    {
                        IsSortable = true
                    },

                    // Content — the actual text to search (full-text + vector)
                    new SearchableField("content"),

                    // Vector field — the embedding for semantic/vector search
                    new SearchField("contentVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        IsSearchable = true,
                        VectorSearchDimensions = EmbeddingDimensions,
                        VectorSearchProfileName = "vector-profile"
                    }
                },

                // Configure vector search (HNSW algorithm for fast approximate nearest-neighbor search)
                VectorSearch = new VectorSearch
                {
                    Profiles =
                    {
                        new VectorSearchProfile("vector-profile", "hnsw-config")
                    },
                    Algorithms =
                    {
                        new HnswAlgorithmConfiguration("hnsw-config")
                        {
                            Parameters = new HnswParameters
                            {
                                Metric = VectorSearchAlgorithmMetric.Cosine,
                                M = 4,
                                EfConstruction = 400,
                                EfSearch = 500
                            }
                        }
                    }
                }
            };

            await _indexClient.CreateOrUpdateIndexAsync(definition, cancellationToken: cancellationToken);
            _logger.LogInformation("Search index {IndexName} created/updated successfully", _indexName);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 400 && ex.Message.Contains("cannot be changed"))
        {
            // Existing index has incompatible schema — delete and recreate
            _logger.LogWarning("Index {IndexName} has incompatible schema, deleting and recreating...", _indexName);
            try
            {
                await _indexClient.DeleteIndexAsync(_indexName, cancellationToken);
                _logger.LogInformation("Deleted old index {IndexName}", _indexName);

                // Wait for deletion to propagate
                await Task.Delay(2000, cancellationToken);

                // Recreate with the correct schema
                return await CreateOrUpdateIndexAsync(cancellationToken);
            }
            catch (RequestFailedException deleteEx)
            {
                _logger.LogError(deleteEx, "Failed to delete/recreate index {IndexName}", _indexName);
                return false;
            }
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to create/update search index {IndexName}: {Message}",
                _indexName, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Uploads documents to the search index in batches.
    /// Uses merge-or-upload: if a document with the same ID exists, it's updated; otherwise created.
    /// </summary>
    public async Task<int> UploadDocumentsAsync(
        List<IndexDocument> documents,
        CancellationToken cancellationToken = default)
    {
        if (documents.Count == 0)
        {
            _logger.LogWarning("No documents to upload");
            return 0;
        }

        _logger.LogInformation("Uploading {Count} documents to index {IndexName}", documents.Count, _indexName);

        var uploaded = 0;
        const int batchSize = 100; // AI Search accepts max 1000 per batch, we use 100 for safety

        for (int i = 0; i < documents.Count; i += batchSize)
        {
            var batch = documents.Skip(i).Take(batchSize).ToList();

            var searchDocuments = batch.Select(doc =>
            {
                var searchDoc = new SearchDocument
                {
                    ["id"] = doc.Id,
                    ["patternSlug"] = doc.PatternSlug,
                    ["title"] = doc.Title,
                    ["category"] = doc.Category,
                    ["chunkType"] = doc.ChunkType,
                    ["chunkIndex"] = doc.ChunkIndex,
                    ["content"] = doc.Content,
                    ["contentVector"] = doc.ContentVector
                };
                return searchDoc;
            }).ToList();

            try
            {
                var result = await _searchClient.MergeOrUploadDocumentsAsync(
                    searchDocuments,
                    cancellationToken: cancellationToken);

                var successCount = result.Value.Results.Count(r => r.Succeeded);
                uploaded += successCount;

                _logger.LogInformation(
                    "Batch {BatchNumber}: {Success}/{Total} documents uploaded",
                    (i / batchSize) + 1, successCount, batch.Count);

                if (result.Value.Results.Any(r => !r.Succeeded))
                {
                    foreach (var failed in result.Value.Results.Where(r => !r.Succeeded))
                    {
                        _logger.LogWarning(
                            "Failed to upload document {Key}: {Message}",
                            failed.Key, failed.ErrorMessage);
                    }
                }
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Batch upload failed at offset {Offset}", i);
                throw;
            }
        }

        _logger.LogInformation("Upload complete: {Uploaded}/{Total} documents indexed", uploaded, documents.Count);
        return uploaded;
    }

    /// <summary>
    /// Deletes all documents from the index for a clean re-index.
    /// Gets all document IDs first, then deletes in batches.
    /// </summary>
    public async Task<int> DeleteAllDocumentsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting all documents from index {IndexName}", _indexName);

        try
        {
            var options = new SearchOptions
            {
                Select = { "id" },
                Size = 1000
            };

            var results = await _searchClient.SearchAsync<SearchDocument>("*", options, cancellationToken);
            var ids = new List<string>();

            await foreach (var result in results.Value.GetResultsAsync())
            {
                if (result.Document.TryGetValue("id", out var id) && id != null)
                {
                    ids.Add(id.ToString()!);
                }
            }

            if (ids.Count == 0)
            {
                _logger.LogInformation("Index is already empty");
                return 0;
            }

            var deleteDocuments = ids.Select(id => new SearchDocument { ["id"] = id }).ToList();
            var result2 = await _searchClient.DeleteDocumentsAsync(
                deleteDocuments,
                cancellationToken: cancellationToken);

            var deleted = result2.Value.Results.Count(r => r.Succeeded);
            _logger.LogInformation("Deleted {Count} documents from index", deleted);
            return deleted;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to delete documents from index");
            throw;
        }
    }

    public async Task<long> GetDocumentCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _searchClient.GetDocumentCountAsync(cancellationToken);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Could not get document count");
            return -1;
        }
    }
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
