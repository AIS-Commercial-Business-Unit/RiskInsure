namespace RiskInsure.FileRetrieval.Domain.ValueObjects;

/// <summary>
/// T030: File pattern with optional date tokens for dynamic path/filename matching.
/// </summary>
public sealed class FilePattern
{
    /// <summary>
    /// Pattern string (may contain date tokens like {yyyy}, {mm}, {dd})
    /// </summary>
    public string Pattern { get; init; }

    /// <summary>
    /// Whether pattern contains date tokens
    /// </summary>
    public bool HasTokens { get; init; }

    public FilePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Pattern cannot be empty", nameof(pattern));

        Pattern = pattern;
        HasTokens = pattern.Contains('{') && pattern.Contains('}');
    }

    /// <summary>
    /// Validate that tokens are not in server/host portion of URL
    /// </summary>
    public void ValidateTokenPlacement(string baseUrl)
    {
        if (!HasTokens)
            return;

        // Tokens should only be in path/filename, not in server name
        if (baseUrl.Contains('{'))
            throw new ArgumentException("Date tokens cannot be used in server/host name", nameof(baseUrl));
    }
}
