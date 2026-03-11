namespace RiskInsure.Modernization.Reindex.Controllers;

using Microsoft.AspNetCore.Mvc;
using RiskInsure.Modernization.Reindex.Services;
using System.Diagnostics;

/// <summary>
/// The Reindex Controller — the "button" that triggers indexing.
///
/// What it does in plain English:
/// 1. POST /api/reindex → reads all 41 pattern JSON files from disk
/// 2. Breaks each pattern into 2-4 smaller text chunks
/// 3. Sends each chunk to Azure OpenAI to get its "embedding" (vector of numbers)
/// 4. Uploads all chunks + embeddings to Azure AI Search
///
/// After this runs, the Chat API can find relevant patterns
/// when users ask questions (that's the "R" in RAG).
///
/// Can be triggered:
/// - Manually via Postman/curl for testing
/// - Via GitHub webhook when pattern files change in the repo
/// - On a schedule (timer trigger in Container Apps)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ReindexController : ControllerBase
{
    private readonly IChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IIndexingService _indexingService;
    private readonly ILogger<ReindexController> _logger;
    private readonly IConfiguration _config;

    public ReindexController(
        IChunkingService chunkingService,
        IEmbeddingService embeddingService,
        IIndexingService indexingService,
        ILogger<ReindexController> logger,
        IConfiguration config)
    {
        _chunkingService = chunkingService;
        _embeddingService = embeddingService;
        _indexingService = indexingService;
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Full reindex: read all pattern files → chunk → embed → upload to AI Search.
    ///
    /// Query parameters:
    /// - clean=true: Delete existing documents before re-indexing (fresh start)
    /// - pattern=strangler-fig-migration: Only index a specific pattern (for testing)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Reindex(
        [FromQuery] bool clean = false,
        [FromQuery] string? pattern = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Reindex started. Clean={Clean}, Pattern={Pattern}",
            clean, pattern ?? "all");

        try
        {
            // Step 1: Ensure the search index exists with the right schema
            _logger.LogInformation("Step 1: Creating/updating search index schema...");
            var indexCreated = await _indexingService.CreateOrUpdateIndexAsync(cancellationToken);
            if (!indexCreated)
            {
                return StatusCode(500, new { error = "Failed to create/update search index" });
            }

            // Step 2: Optionally clean existing documents
            var deletedCount = 0;
            if (clean)
            {
                _logger.LogInformation("Step 2: Cleaning existing documents...");
                deletedCount = await _indexingService.DeleteAllDocumentsAsync(cancellationToken);
                _logger.LogInformation("Deleted {Count} existing documents", deletedCount);

                // Wait for deletion to propagate in the search index
                await Task.Delay(2000, cancellationToken);
            }

            // Step 3: Find and read pattern files
            _logger.LogInformation("Step 3: Reading pattern files...");
            var contentPath = ResolveContentPath();
            var patternFiles = GetPatternFiles(contentPath, pattern);

            if (patternFiles.Count == 0)
            {
                return NotFound(new
                {
                    error = "No pattern files found",
                    searchPath = contentPath,
                    filter = pattern
                });
            }

            _logger.LogInformation("Found {Count} pattern files to process", patternFiles.Count);

            // Step 4: Chunk all patterns
            _logger.LogInformation("Step 4: Chunking patterns...");
            var allChunks = new List<PatternChunk>();

            foreach (var file in patternFiles)
            {
                var json = await System.IO.File.ReadAllTextAsync(file, cancellationToken);
                var slug = Path.GetFileNameWithoutExtension(file);
                var chunks = _chunkingService.ChunkPattern(json, slug);
                allChunks.AddRange(chunks);
            }

            _logger.LogInformation(
                "Chunking complete: {PatternCount} patterns → {ChunkCount} chunks",
                patternFiles.Count, allChunks.Count);

            // Step 5: Embed all chunks
            _logger.LogInformation("Step 5: Embedding {Count} chunks...", allChunks.Count);
            var texts = allChunks.Select(c => c.Content).ToList();
            var embeddings = await _embeddingService.EmbedBatchAsync(texts, cancellationToken);

            // Step 6: Build index documents (chunk + embedding paired together)
            _logger.LogInformation("Step 6: Building index documents...");
            var indexDocuments = new List<IndexDocument>();

            for (int i = 0; i < allChunks.Count; i++)
            {
                var chunk = allChunks[i];
                indexDocuments.Add(new IndexDocument
                {
                    Id = chunk.Id,
                    PatternSlug = chunk.PatternSlug,
                    Title = chunk.Title,
                    Category = chunk.Category,
                    ChunkType = chunk.ChunkType,
                    ChunkIndex = chunk.ChunkIndex,
                    Content = chunk.Content,
                    ContentVector = embeddings[i]
                });
            }

            // Step 7: Upload to AI Search
            _logger.LogInformation("Step 7: Uploading to AI Search...");
            var uploadedCount = await _indexingService.UploadDocumentsAsync(indexDocuments, cancellationToken);

            // Wait for indexing to propagate
            await Task.Delay(2000, cancellationToken);

            // Get final document count
            var totalDocuments = await _indexingService.GetDocumentCountAsync(cancellationToken);

            stopwatch.Stop();

            var result = new
            {
                status = "success",
                patternsProcessed = patternFiles.Count,
                chunksCreated = allChunks.Count,
                documentsUploaded = uploadedCount,
                documentsDeleted = deletedCount,
                totalDocumentsInIndex = totalDocuments,
                elapsedSeconds = stopwatch.Elapsed.TotalSeconds,
                patterns = patternFiles.Select(Path.GetFileNameWithoutExtension).ToList()
            };

            _logger.LogInformation(
                "Reindex complete: {Patterns} patterns, {Chunks} chunks, {Uploaded} uploaded in {Seconds:F1}s",
                result.patternsProcessed, result.chunksCreated, result.documentsUploaded, result.elapsedSeconds);

            return Ok(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Reindex failed after {Seconds:F1}s", stopwatch.Elapsed.TotalSeconds);
            return StatusCode(500, new
            {
                error = "Reindex failed",
                message = ex.Message,
                elapsedSeconds = stopwatch.Elapsed.TotalSeconds
            });
        }
    }

    /// <summary>
    /// GET /api/reindex/status — Check how many documents are currently in the index.
    /// Useful for verifying that indexing worked without re-running it.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var documentCount = await _indexingService.GetDocumentCountAsync(cancellationToken);
        var contentPath = ResolveContentPath();
        var patternFiles = GetPatternFiles(contentPath, null);

        return Ok(new
        {
            indexName = _config["AzureSearch:IndexName"] ?? "modernization-patterns",
            documentsInIndex = documentCount,
            patternFilesOnDisk = patternFiles.Count,
            contentPath = contentPath,
            status = documentCount > 0 ? "indexed" : "empty",
            patterns = patternFiles.Select(Path.GetFileNameWithoutExtension).ToList()
        });
    }

    /// <summary>
    /// POST /api/reindex/single/{slug} — Index a single pattern (useful for testing).
    /// Example: POST /api/reindex/single/strangler-fig-migration
    /// </summary>
    [HttpPost("single/{slug}")]
    public async Task<IActionResult> ReindexSingle(
        string slug,
        CancellationToken cancellationToken)
    {
        return await Reindex(clean: false, pattern: slug, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Find the content/patterns directory.
    /// Works both when running locally (dev) and from the project directory.
    /// </summary>
    private string ResolveContentPath()
    {
        // Check config first
        var configuredPath = _config["Reindex:ContentPath"];
        if (!string.IsNullOrEmpty(configuredPath) && Directory.Exists(configuredPath))
        {
            return configuredPath;
        }

        // Walk up from the current directory to find the content/patterns folder
        var candidates = new[]
        {
            // When running from Api/reindex:
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "content", "patterns"),
            // When running from repo root:
            Path.Combine(Directory.GetCurrentDirectory(), "platform", "modernizationpatterns", "content", "patterns"),
            // Absolute fallback (common dev path):
            @"c:\RiskInsure\RiskInsure\platform\modernizationpatterns\content\patterns"
        };

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (Directory.Exists(resolved))
            {
                _logger.LogInformation("Content path resolved to: {Path}", resolved);
                return resolved;
            }
        }

        // Default — may not exist, caller handles the error
        var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "content", "patterns");
        _logger.LogWarning("Content path not found, defaulting to: {Path}", defaultPath);
        return defaultPath;
    }

    /// <summary>Get pattern JSON files, optionally filtered to a single pattern by slug</summary>
    private static List<string> GetPatternFiles(string contentPath, string? patternFilter)
    {
        if (!Directory.Exists(contentPath))
        {
            return new List<string>();
        }

        var files = Directory.GetFiles(contentPath, "*.json").ToList();

        if (!string.IsNullOrEmpty(patternFilter))
        {
            files = files
                .Where(f => Path.GetFileNameWithoutExtension(f)
                    .Equals(patternFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return files.OrderBy(f => f).ToList();
    }
}
