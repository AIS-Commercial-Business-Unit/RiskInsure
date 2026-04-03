namespace RiskInsure.Modernization.Reindex.Controllers;

using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Mvc;
using RiskInsure.Modernization.Reindex.Services;
using System.Diagnostics;
using System.Text;
using UglyToad.PdfPig;

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

            // Step 3: Find and read pattern/inbox/agentic files
            _logger.LogInformation("Step 3: Reading pattern, inbox, and agentic files...");
            var patternsPath = ResolvePatternsPath();
            var inboxPath = ResolveInboxPath();
            var agenticPath = ResolveAgenticPath();
            var patternFiles = GetPatternFiles(patternsPath, pattern);
            var inboxFiles = string.IsNullOrEmpty(pattern) ? GetInboxFiles(inboxPath) : new List<string>();
            var agenticFiles = string.IsNullOrEmpty(pattern) ? GetAgenticFiles(agenticPath) : new List<string>();

            if (patternFiles.Count == 0 && inboxFiles.Count == 0 && agenticFiles.Count == 0)
            {
                return NotFound(new
                {
                    error = "No pattern, inbox, or agentic files found",
                    patternsPath,
                    inboxPath,
                    agenticPath,
                    filter = pattern
                });
            }

            _logger.LogInformation(
                "Found {PatternCount} pattern files, {InboxCount} inbox files, and {AgenticCount} agentic files to process",
                patternFiles.Count,
                inboxFiles.Count,
                agenticFiles.Count);

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

            foreach (var file in inboxFiles)
            {
                var text = await ExtractInboxTextAsync(file, cancellationToken);
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("Skipping inbox file {File} because no text could be extracted", file);
                    continue;
                }

                var slug = Path.GetFileNameWithoutExtension(file);
                var sourceType = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
                var chunks = _chunkingService.ChunkInboxDocument(text, slug, sourceType);
                allChunks.AddRange(chunks);
            }

            // Process agentic JSON files (same chunking as patterns)
            foreach (var file in agenticFiles)
            {
                var json = await System.IO.File.ReadAllTextAsync(file, cancellationToken);
                var slug = $"agentic-{Path.GetFileNameWithoutExtension(file)}";
                var chunks = _chunkingService.ChunkPattern(json, slug);
                allChunks.AddRange(chunks);
            }

            _logger.LogInformation(
                "Chunking complete: {PatternCount} patterns + {InboxCount} inbox + {AgenticCount} agentic → {ChunkCount} chunks",
                patternFiles.Count,
                inboxFiles.Count,
                agenticFiles.Count,
                allChunks.Count);

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
                inboxDocumentsProcessed = inboxFiles.Count,
                agenticDocumentsProcessed = agenticFiles.Count,
                chunksCreated = allChunks.Count,
                documentsUploaded = uploadedCount,
                documentsDeleted = deletedCount,
                totalDocumentsInIndex = totalDocuments,
                elapsedSeconds = stopwatch.Elapsed.TotalSeconds,
                patterns = patternFiles.Select(Path.GetFileNameWithoutExtension).ToList(),
                inboxDocuments = inboxFiles.Select(Path.GetFileName).ToList(),
                agenticDocuments = agenticFiles.Select(Path.GetFileName).ToList()
            };

            _logger.LogInformation(
                "Reindex complete: {Patterns} patterns + {Inbox} inbox + {Agentic} agentic, {Chunks} chunks, {Uploaded} uploaded in {Seconds:F1}s",
                result.patternsProcessed, result.inboxDocumentsProcessed, result.agenticDocumentsProcessed,
                result.chunksCreated, result.documentsUploaded, result.elapsedSeconds);

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
        var patternsPath = ResolvePatternsPath();
        var inboxPath = ResolveInboxPath();
        var patternFiles = GetPatternFiles(patternsPath, null);
        var inboxFiles = GetInboxFiles(inboxPath);

        return Ok(new
        {
            indexName = _config["AzureSearch:IndexName"] ?? "modernization-patterns",
            documentsInIndex = documentCount,
            patternFilesOnDisk = patternFiles.Count,
            inboxFilesOnDisk = inboxFiles.Count,
            patternsPath,
            inboxPath,
            status = documentCount > 0 ? "indexed" : "empty",
            patterns = patternFiles.Select(Path.GetFileNameWithoutExtension).ToList(),
            inboxDocuments = inboxFiles.Select(Path.GetFileName).ToList()
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
    private string ResolvePatternsPath()
    {
        // Check config first
        var configuredPath = _config["Reindex:PatternsPath"] ?? _config["Reindex:ContentPath"];
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

    /// <summary>
    /// Find the content/_inbox directory for incoming source documents.
    /// </summary>
    private string ResolveInboxPath()
    {
        var configuredPath = _config["Reindex:InboxPath"];
        if (!string.IsNullOrEmpty(configuredPath) && Directory.Exists(configuredPath))
        {
            return configuredPath;
        }

        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "content", "_inbox"),
            Path.Combine(Directory.GetCurrentDirectory(), "platform", "modernizationpatterns", "content", "_inbox"),
            @"c:\RiskInsure\RiskInsure\platform\modernizationpatterns\content\_inbox"
        };

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (Directory.Exists(resolved))
            {
                _logger.LogInformation("Inbox path resolved to: {Path}", resolved);
                return resolved;
            }
        }

        var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "content", "_inbox");
        _logger.LogWarning("Inbox path not found, defaulting to: {Path}", defaultPath);
        return defaultPath;
    }

    /// <summary>
    /// Find the content/agentic directory for agentic JSON files.
    /// </summary>
    private string ResolveAgenticPath()
    {
        var configuredPath = _config["Reindex:AgenticPath"];
        if (!string.IsNullOrEmpty(configuredPath) && Directory.Exists(configuredPath))
        {
            return configuredPath;
        }

        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "content", "agentic"),
            Path.Combine(Directory.GetCurrentDirectory(), "platform", "modernizationpatterns", "content", "agentic"),
            @"c:\RiskInsure\RiskInsure\platform\modernizationpatterns\content\agentic"
        };

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (Directory.Exists(resolved))
            {
                _logger.LogInformation("Agentic path resolved to: {Path}", resolved);
                return resolved;
            }
        }

        var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "content", "agentic");
        _logger.LogWarning("Agentic path not found, defaulting to: {Path}", defaultPath);
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

    /// <summary>Get agentic JSON files from the agentic content folder</summary>
    private static List<string> GetAgenticFiles(string agenticPath)
    {
        if (!Directory.Exists(agenticPath))
        {
            return new List<string>();
        }

        return Directory.GetFiles(agenticPath, "*.json")
            .OrderBy(f => f)
            .ToList();
    }

    private static List<string> GetInboxFiles(string inboxPath)
    {
        if (!Directory.Exists(inboxPath))
        {
            return new List<string>();
        }

        var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".json", ".md", ".markdown", ".txt", ".docx", ".pdf"
        };

        return Directory.GetFiles(inboxPath, "*", SearchOption.AllDirectories)
            .Where(file => supportedExtensions.Contains(Path.GetExtension(file)))
            .OrderBy(file => file)
            .ToList();
    }

    private static async Task<string?> ExtractInboxTextAsync(string filePath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".json" or ".md" or ".markdown" or ".txt" => await System.IO.File.ReadAllTextAsync(filePath, cancellationToken),
            ".pdf" => ExtractPdfText(filePath),
            ".docx" => ExtractDocxText(filePath),
            _ => null
        };
    }

    private static string? ExtractPdfText(string filePath)
    {
        var builder = new StringBuilder();
        using var pdf = PdfDocument.Open(filePath);

        foreach (var page in pdf.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }

    private static string? ExtractDocxText(string filePath)
    {
        using var document = WordprocessingDocument.Open(filePath, false);
        return document.MainDocumentPart?.Document?.Body?.InnerText;
    }
}
