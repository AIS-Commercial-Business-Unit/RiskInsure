namespace RiskInsure.Modernization.Reindex.Services;

using Microsoft.Extensions.Logging;
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
}

public class ChunkingService : IChunkingService
{
    private readonly ILogger<ChunkingService> _logger;
    private const int TargetChunkTokens = 500;
    private const int OverlapTokens = 100;

    public ChunkingService(ILogger<ChunkingService> logger)
    {
        _logger = logger;
    }

    public List<PatternChunk> ChunkPattern(string patternJson, string patternSlug)
    {
        var chunks = new List<PatternChunk>();

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
                ChunkIndex = 0
            });

            // --- Chunk 2: Implementation details (technologies + gotchas) ---
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
            if (implContent.Split('\n').Length > 4) // Only add if there's meaningful content beyond headers
            {
                chunks.Add(new PatternChunk
                {
                    Id = $"{patternSlug}_implementation",
                    PatternSlug = patternSlug,
                    Title = title,
                    Category = category,
                    ChunkType = "implementation",
                    Content = implContent,
                    ChunkIndex = 1
                });
            }

            // --- Chunk 3: Complexity + real-world example ---
            var complexityBuilder = new StringBuilder();
            complexityBuilder.AppendLine($"# {title} — Complexity & Real-World Example");
            complexityBuilder.AppendLine($"Category: {category}");
            complexityBuilder.AppendLine();

            if (root.TryGetProperty("complexity", out var complexity))
            {
                complexityBuilder.AppendLine("## Complexity Assessment");
                AppendField(complexityBuilder, complexity, "level", "Level");
                AppendField(complexityBuilder, complexity, "rationale", "Rationale");
                AppendField(complexityBuilder, complexity, "teamImpact", "Team Impact");
                AppendField(complexityBuilder, complexity, "skillDemand", "Skill Demand");
                AppendField(complexityBuilder, complexity, "operationalDemand", "Operational Demand");
                AppendField(complexityBuilder, complexity, "toolingDemand", "Tooling Demand");
            }

            if (root.TryGetProperty("realWorldExample", out var example))
            {
                complexityBuilder.AppendLine();
                complexityBuilder.AppendLine("## Real-World Example");
                AppendField(complexityBuilder, example, "context", "Context");
                AppendField(complexityBuilder, example, "approach", "Approach");
                AppendField(complexityBuilder, example, "outcome", "Outcome");
            }

            AppendStringArray(complexityBuilder, root, "relatedPatterns", "Related Patterns");
            AppendStringArray(complexityBuilder, root, "tags", "Tags");

            var complexityContent = complexityBuilder.ToString().Trim();
            if (complexityContent.Split('\n').Length > 4)
            {
                chunks.Add(new PatternChunk
                {
                    Id = $"{patternSlug}_complexity",
                    PatternSlug = patternSlug,
                    Title = title,
                    Category = category,
                    ChunkType = "complexity",
                    Content = complexityContent,
                    ChunkIndex = 2
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
                            ChunkIndex = 10 + i
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
