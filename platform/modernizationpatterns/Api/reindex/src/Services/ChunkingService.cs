namespace RiskInsure.Modernization.Reindex.Services;

using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public interface IChunkingService
{
    List<PatternChunk> ChunkPattern(string patternJson, string patternSlug);

    List<PatternChunk> ChunkInboxDocument(string documentText, string documentSlug, string sourceType);
}

public class ChunkingService : IChunkingService
{
    private readonly ILogger<ChunkingService> _logger;

    private const int TargetChunkTokens = 1200;
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

            var fullCategory = $"{category}/{subcategory}";

            // --- Overview ---
            var overviewBuilder = new StringBuilder();
            overviewBuilder.AppendLine($"# {title}");
            overviewBuilder.AppendLine($"Category: {fullCategory}");
            overviewBuilder.AppendLine();
            overviewBuilder.AppendLine("## Summary");
            overviewBuilder.AppendLine(summary);

            chunks.Add(new PatternChunk
            {
                Id = $"{patternSlug}_overview",
                PatternSlug = patternSlug,
                Title = title,
                Category = fullCategory,
                ChunkType = "overview",
                Content = overviewBuilder.ToString().Trim(),
                ChunkIndex = chunkIndex++
            });

            // --- Decision Guidance (fine-grained) ---
            if (root.TryGetProperty("decisionGuidance", out var guidance))
            {
                if (guidance.TryGetProperty("whenToUse", out var whenToUse))
                {
                    chunks.Add(new PatternChunk
                    {
                        Id = $"{patternSlug}_whenToUse",
                        PatternSlug = patternSlug,
                        Title = title,
                        Category = fullCategory,
                        ChunkType = "whenToUse",
                        Content = $"# {title} — When To Use\nCategory: {fullCategory}\n\n" +
                                  string.Join("\n", whenToUse.EnumerateArray().Select(x => "- " + x.GetString())),
                        ChunkIndex = chunkIndex++
                    });
                }

                if (guidance.TryGetProperty("whenNotToUse", out var whenNotToUse))
                {
                    chunks.Add(new PatternChunk
                    {
                        Id = $"{patternSlug}_whenNotToUse",
                        PatternSlug = patternSlug,
                        Title = title,
                        Category = fullCategory,
                        ChunkType = "whenNotToUse",
                        Content = $"# {title} — When NOT To Use\nCategory: {fullCategory}\n\n" +
                                  string.Join("\n", whenNotToUse.EnumerateArray().Select(x => "- " + x.GetString())),
                        ChunkIndex = chunkIndex++
                    });
                }
            }

            // --- Implementation ---
            var implBuilder = new StringBuilder();
            implBuilder.AppendLine($"# {title} — Implementation");
            implBuilder.AppendLine($"Category: {fullCategory}");
            implBuilder.AppendLine();

            AppendStringArray(implBuilder, root, "enablingTechnologies", "Technologies");

            var implContent = implBuilder.ToString().Trim();
            if (implContent.Length > 100)
            {
                chunks.Add(new PatternChunk
                {
                    Id = $"{patternSlug}_implementation",
                    PatternSlug = patternSlug,
                    Title = title,
                    Category = fullCategory,
                    ChunkType = "implementation",
                    Content = implContent,
                    ChunkIndex = chunkIndex++
                });
            }

            _logger.LogInformation("Chunked pattern {PatternSlug}: {Count}", patternSlug, chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chunking failed for {PatternSlug}", patternSlug);

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
            return chunks;

        var splitChunks = SplitTextIntoChunks(documentText);

        for (int i = 0; i < splitChunks.Count; i++)
        {
            chunks.Add(new PatternChunk
            {
                Id = ToSafeChunkId($"{documentSlug}_{i}"),
                PatternSlug = documentSlug,
                Title = documentSlug,
                Category = "inbox",
                ChunkType = sourceType,
                Content = $@"
# {documentSlug}
Category: inbox
SourceType: {sourceType}
Chunk: {i}

{splitChunks[i]}
".Trim(),
                ChunkIndex = i
            });
        }

        return chunks;
    }

    // ---------- NEW SMART SPLIT ----------
    private static List<string> SplitTextIntoChunks(string text)
    {
        var sections = SplitByHeadings(text);
        var chunks = new List<string>();

        foreach (var section in sections)
        {
            var sentences = section.Split('.', StringSplitOptions.RemoveEmptyEntries);

            var current = new StringBuilder();

            foreach (var sentence in sentences)
            {
                if (current.Length > 4000)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                }

                current.Append(sentence).Append(". ");
            }

            if (current.Length > 0)
                chunks.Add(current.ToString());
        }

        return chunks;
    }

    private static List<string> SplitByHeadings(string text)
    {
        var sections = new List<string>();
        var lines = text.Split('\n');

        var current = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.StartsWith("#") ||
                line.StartsWith("Chapter", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Section", StringComparison.OrdinalIgnoreCase))
            {
                if (current.Length > 0)
                {
                    sections.Add(current.ToString());
                    current.Clear();
                }
            }

            current.AppendLine(line);
        }

        if (current.Length > 0)
            sections.Add(current.ToString());

        return sections;
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) ? value.GetString() : null;

    private static void AppendStringArray(StringBuilder sb, JsonElement element, string property, string label)
    {
        if (!element.TryGetProperty(property, out var array)) return;

        sb.AppendLine($"## {label}");
        foreach (var item in array.EnumerateArray())
        {
            sb.AppendLine($"- {item.GetString()}");
        }
    }

    private static string ToSafeChunkId(string value)
    {
        var cleaned = new string(value.Where(char.IsLetterOrDigit).ToArray());
        return cleaned.Length > 100 ? cleaned.Substring(0, 100) : cleaned;
    }
}

public class PatternChunk
{
    public required string Id { get; init; }
    public required string PatternSlug { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string ChunkType { get; init; }
    public required string Content { get; init; }
    public required int ChunkIndex { get; init; }
}
