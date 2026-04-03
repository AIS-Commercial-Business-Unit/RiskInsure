namespace RiskInsure.Modernization.Reindex.Services;

using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

public class ChunkingService : IChunkingService
{
    private readonly ILogger<ChunkingService> _logger;
    // Increased chunk size to reduce API calls (was 500, now 2000)
    // Fewer, larger chunks = fewer embedding API calls = less rate limiting
    private const int TargetChunkTokens = 2000;
    private const int OverlapTokens = 150;

    public ChunkingService(ILogger<ChunkingService> logger)
    {
        _logger = logger;
    }

    public List<PatternChunk> ChunkPattern(string patternJson, string patternSlug)
    {
        var chunks = new List<PatternChunk>();
        int chunkIndex = 0;

        try
        {
            using var doc = JsonDocument.Parse(patternJson);
            var root = doc.RootElement;

            var title = GetString(root, "title") ?? patternSlug;
            var category = GetString(root, "category") ?? "unknown";
            var subcategory = GetString(root, "subcategory") ?? "";
            var summary = GetString(root, "summary") ?? "";

            // --- Chunk 1: Overview (summary + decision guidance) ---
            var overviewBuilder = new StringBuilder();
            overviewBuilder.AppendLine($"# {title}");
            overviewBuilder.AppendLine($"Category: {category} / {subcategory}");
            overviewBuilder.AppendLine();
            overviewBuilder.AppendLine($"## Summary");
            overviewBuilder.AppendLine(summary);

            if (root.TryGetProperty("decisionGuidance", out var guidance))
            {
                overviewBuilder.AppendLine();
                overviewBuilder.AppendLine($"## Decision Guidance");

                if (guidance.TryGetProperty("problemSolved", out var problem))
                {
                    overviewBuilder.AppendLine($"Problem Solved: {problem.GetString()}");
                }

                AppendStringArray(overviewBuilder, guidance, "whenToUse", "When to Use");
                AppendStringArray(overviewBuilder, guidance, "whenNotToUse", "When NOT to Use");
            }

            chunks.Add(new PatternChunk
            {
                Id = $"{patternSlug}_overview",
                PatternSlug = patternSlug,
                Title = title,
                Category = category,
                ChunkType = "overview",
                Content = overviewBuilder.ToString().Trim(),
                ChunkIndex = chunkIndex++
            });

            // --- Chunk 2: Starter Diagram (if exists) ---
            if (root.TryGetProperty("starterDiagram", out var diagram))
            {
                var diagramBuilder = new StringBuilder();
                diagramBuilder.AppendLine($"# {title} — Starter Diagram");
                diagramBuilder.AppendLine($"Category: {category}");
                diagramBuilder.AppendLine();

                if (diagram.TryGetProperty("title", out var diagramTitle))
                {
                    diagramBuilder.AppendLine($"## Diagram Title: {diagramTitle.GetString()}");
                }

                if (diagram.TryGetProperty("description", out var diagramDesc))
                {
                    diagramBuilder.AppendLine();
                    diagramBuilder.AppendLine($"## Description");
                    diagramBuilder.AppendLine(diagramDesc.GetString());
                }

                if (diagram.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array)
                {
                    diagramBuilder.AppendLine();
                    diagramBuilder.AppendLine("## Key Components/Nodes");
                    foreach (var node in nodes.EnumerateArray())
                    {
                        if (node.ValueKind == JsonValueKind.String)
                        {
                            diagramBuilder.AppendLine($"- {node.GetString()}");
                        }
                    }
                }

                chunks.Add(new PatternChunk
                {
                    Id = $"{patternSlug}_diagram",
                    PatternSlug = patternSlug,
                    Title = title,
                    Category = category,
                    ChunkType = "diagram",
                    Content = diagramBuilder.ToString().Trim(),
                    ChunkIndex = chunkIndex++
                });
            }

            // --- Chunk 3: Implementation details (technologies + gotchas) ---
            var implBuilder = new StringBuilder();
            implBuilder.AppendLine($"# {title} — Implementation Details");
            implBuilder.AppendLine($"Category: {category}");
            implBuilder.AppendLine();

            AppendStringArray(implBuilder, root, "enablingTechnologies", "Enabling Technologies");

            if (root.TryGetProperty("thingsToWatchOutFor", out var watchOut))
            {
                implBuilder.AppendLine();
                implBuilder.AppendLine("## Things to Watch Out For");

                AppendStringArray(implBuilder, watchOut, "gotchas", "Gotchas");

                if (watchOut.TryGetProperty("opinionatedGuidance", out var opinionated))
                {
                    implBuilder.AppendLine();
                    implBuilder.AppendLine($"## Opinionated Guidance");
                    implBuilder.AppendLine(opinionated.GetString());
                }
            }

            var implContent = implBuilder.ToString().Trim();
            if (implContent.Split('\n').Length > 4)
            {
                chunks.Add(new PatternChunk
                {
                    Id = $"{patternSlug}_implementation",
                    PatternSlug = patternSlug,
                    Title = title,
                    Category = category,
                    ChunkType = "implementation",
                    Content = implContent,
                    ChunkIndex = chunkIndex++
                });
            }

            // --- Chunk 4: Details (Complexity + Example + Related combined into one chunk) ---
            // Consolidating these small sections reduces chunk count significantly
            var detailsBuilder = new StringBuilder();
            detailsBuilder.AppendLine($"# {title} — Details & Context");
            detailsBuilder.AppendLine($"Category: {category}");
            detailsBuilder.AppendLine();

            // Add complexity assessment
            if (root.TryGetProperty("complexity", out var complexity))
            {
                detailsBuilder.AppendLine("## Complexity Assessment");
                AppendField(detailsBuilder, complexity, "level", "Level");
                AppendField(detailsBuilder, complexity, "rationale", "Rationale");
                AppendField(detailsBuilder, complexity, "teamImpact", "Team Impact");
                AppendField(detailsBuilder, complexity, "skillDemand", "Skill Demand");
                AppendField(detailsBuilder, complexity, "operationalDemand", "Operational Demand");
                AppendField(detailsBuilder, complexity, "toolingDemand", "Tooling Demand");
                detailsBuilder.AppendLine();
            }

            // Add real-world example
            if (root.TryGetProperty("realWorldExample", out var example))
            {
                detailsBuilder.AppendLine("## Real-World Example");
                AppendField(detailsBuilder, example, "context", "Context");
                AppendField(detailsBuilder, example, "approach", "Approach");
                AppendField(detailsBuilder, example, "outcome", "Outcome");
                detailsBuilder.AppendLine();
            }

            // Add related patterns and further reading
            AppendStringArray(detailsBuilder, root, "relatedPatterns", "Related Patterns");
            AppendStringArray(detailsBuilder, root, "tags", "Tags");

            if (root.TryGetProperty("furtherReading", out var furtherReading) && furtherReading.ValueKind == JsonValueKind.Array)
            {
                detailsBuilder.AppendLine();
                detailsBuilder.AppendLine("## Further Reading");
                foreach (var readingItem in furtherReading.EnumerateArray())
                {
                    if (readingItem.TryGetProperty("title", out var readingTitle))
                    {
                        detailsBuilder.Append($"- **{readingTitle.GetString()}**");
                        if (readingItem.TryGetProperty("link", out var readingLink))
                        {
                            detailsBuilder.AppendLine($": {readingLink.GetString()}");
                        }
                        else
                        {
                            detailsBuilder.AppendLine();
                        }
                    }
                }
            }

            var detailsContent = detailsBuilder.ToString().Trim();
            if (detailsContent.Split('\n').Length > 4) // Only create if has meaningful content
            {
                chunks.Add(new PatternChunk
                {
                    Id = $"{patternSlug}_details",
                    PatternSlug = patternSlug,
                    Title = title,
                    Category = category,
                    ChunkType = "details",
                    Content = detailsContent,
                    ChunkIndex = chunkIndex++
                });
            }

            // --- Handle very long opinionated guidance by splitting further ---
            if (root.TryGetProperty("thingsToWatchOutFor", out var watchOutLong) &&
                watchOutLong.TryGetProperty("opinionatedGuidance", out var longGuidance))
            {
                var guidanceText = longGuidance.GetString() ?? "";
                if (EstimateTokens(guidanceText) > TargetChunkTokens)
                {
                    var guidanceChunks = SplitTextIntoChunks(guidanceText, TargetChunkTokens, OverlapTokens);
                    for (int i = 0; i < guidanceChunks.Count; i++)
                    {
                        chunks.Add(new PatternChunk
                        {
                            Id = $"{patternSlug}_guidance_{i}",
                            PatternSlug = patternSlug,
                            Title = title,
                            Category = category,
                            ChunkType = "guidance",
                            Content = $"# {title} — Detailed Guidance (Part {i + 1})\nCategory: {category}\n\n{guidanceChunks[i]}",
                            ChunkIndex = 100 + i
                        });
                    }
                }
            }

            _logger.LogInformation(
                "Chunked pattern {PatternSlug}: {ChunkCount} chunks produced",
                patternSlug, chunks.Count);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse pattern JSON for {PatternSlug}", patternSlug);
            // Fallback: treat the entire JSON as a single chunk
            chunks.Add(new PatternChunk
            {
                Id = $"{patternSlug}_raw",
                PatternSlug = patternSlug,
                Title = patternSlug,
                Category = "unknown",
                ChunkType = "raw",
                Content = patternJson,
                ChunkIndex = 0
            });
        }

        return chunks;
    }

    public List<PatternChunk> ChunkInboxDocument(string documentText, string documentSlug, string sourceType)
    {
        var chunks = new List<PatternChunk>();
        if (string.IsNullOrWhiteSpace(documentText))
        {
            return chunks;
        }

        var safeSlug = string.IsNullOrWhiteSpace(documentSlug) ? "inbox-document" : documentSlug;
        var title = safeSlug.Replace('-', ' ').Replace('_', ' ');
        var splitChunks = SplitTextIntoChunks(documentText, TargetChunkTokens, OverlapTokens);

        for (var i = 0; i < splitChunks.Count; i++)
        {
            chunks.Add(new PatternChunk
            {
                Id = ToSafeChunkId($"inbox_{safeSlug}_{i}"),
                PatternSlug = $"inbox-{safeSlug}",
                Title = title,
                Category = "inbox",
                ChunkType = $"inbox-{sourceType}",
                Content = $"# {title}\nCategory: inbox\nSourceType: {sourceType}\n\n{splitChunks[i]}",
                ChunkIndex = i
            });
        }

        _logger.LogInformation(
            "Chunked inbox document {DocumentSlug}: {ChunkCount} chunks produced",
            safeSlug, chunks.Count);

        return chunks;
    }

    /// <summary>
    /// Splits long text into overlapping chunks by sentence boundaries.
    /// Overlap ensures context isn't lost at chunk boundaries.
    /// </summary>
    private static List<string> SplitTextIntoChunks(string text, int targetTokens, int overlapTokens)
    {
        var sentences = SplitIntoSentences(text);
        var chunks = new List<string>();
        var current = new StringBuilder();
        var currentTokens = 0;
        var overlapBuffer = new Queue<string>();

        foreach (var sentence in sentences)
        {
            var sentenceTokens = EstimateTokens(sentence);

            if (currentTokens + sentenceTokens > targetTokens && currentTokens > 0)
            {
                chunks.Add(current.ToString().Trim());

                // Start new chunk with overlap from end of previous
                current.Clear();
                currentTokens = 0;

                foreach (var overlap in overlapBuffer)
                {
                    current.Append(overlap).Append(' ');
                    currentTokens += EstimateTokens(overlap);
                }
            }

            current.Append(sentence).Append(' ');
            currentTokens += sentenceTokens;

            overlapBuffer.Enqueue(sentence);
            while (EstimateTokens(string.Join(" ", overlapBuffer)) > overlapTokens)
            {
                overlapBuffer.Dequeue();
            }
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString().Trim());
        }

        return chunks;
    }

    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var current = new StringBuilder();

        foreach (var ch in text)
        {
            current.Append(ch);
            if (ch is '.' or '!' or '?' && current.Length > 20)
            {
                sentences.Add(current.ToString().Trim());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            sentences.Add(current.ToString().Trim());
        }

        return sentences;
    }

    /// <summary>Rough token estimate: ~4 characters per token for English text</summary>
    private static int EstimateTokens(string text) => text.Length / 4;

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) ? value.GetString() : null;
    }

    private static void AppendField(StringBuilder sb, JsonElement element, string property, string label)
    {
        if (element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
        {
            sb.AppendLine($"- **{label}**: {value.GetString()}");
        }
    }

    private static void AppendStringArray(StringBuilder sb, JsonElement element, string property, string label)
    {
        if (!element.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
            return;

        sb.AppendLine();
        sb.AppendLine($"## {label}");
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                sb.AppendLine($"- {item.GetString()}");
            }
        }
    }

    private static string ToSafeChunkId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "chunk_0";
        }

        var normalized = value.ToLowerInvariant();
        var cleaned = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-' or '=')
            {
                cleaned.Append(ch);
            }
            else
            {
                cleaned.Append('_');
            }
        }

        var collapsed = cleaned.ToString().Trim('_');
        while (collapsed.Contains("__", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("__", "_", StringComparison.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(collapsed))
        {
            collapsed = "chunk";
        }

        if (collapsed.Length <= 120)
        {
            return collapsed;
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var hash = Convert.ToHexString(hashBytes[..8]).ToLowerInvariant();
        var prefix = collapsed[..Math.Min(96, collapsed.Length)].TrimEnd('_');
        return $"{prefix}_{hash}";
    }
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
