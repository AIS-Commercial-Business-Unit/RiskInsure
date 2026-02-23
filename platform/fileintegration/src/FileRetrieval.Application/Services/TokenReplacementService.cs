using System.Text.RegularExpressions;

namespace RiskInsure.FileRetrieval.Application.Services;

/// <summary>
/// Service for replacing date-based tokens in file path and filename patterns.
/// Supports tokens: {yyyy}, {yy}, {mm}, {dd}
/// </summary>
public class TokenReplacementService
{
    private static readonly Regex TokenPattern = new(@"\{(yyyy|yy|mm|dd)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] ValidTokens = { "{yyyy}", "{yy}", "{mm}", "{dd}" };

    /// <summary>
    /// Replaces tokens in a pattern with values from the provided date.
    /// </summary>
    /// <param name="pattern">Pattern containing tokens like {yyyy}, {mm}, {dd}</param>
    /// <param name="date">Date to use for token replacement</param>
    /// <returns>Pattern with tokens replaced by actual date values</returns>
    public string ReplaceTokens(string pattern, DateTimeOffset date)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return pattern;
        }

        var result = pattern;
        result = result.Replace("{yyyy}", date.Year.ToString("D4"), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{yy}", (date.Year % 100).ToString("D2"), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{mm}", date.Month.ToString("D2"), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{dd}", date.Day.ToString("D2"), StringComparison.OrdinalIgnoreCase);

        return result;
    }

    /// <summary>
    /// Validates that tokens are not present in server/host portion of a path or URL.
    /// Tokens should only appear in path and filename portions.
    /// </summary>
    /// <param name="pattern">Pattern to validate</param>
    /// <param name="isServerOrHost">True if validating server/host portion</param>
    /// <returns>True if valid, false if tokens found in invalid location</returns>
    public bool ValidateTokenPosition(string pattern, bool isServerOrHost)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }

        // If this is server/host portion, tokens should NOT be present
        if (isServerOrHost)
        {
            return !ContainsTokens(pattern);
        }

        // For path/filename portions, tokens are allowed
        return true;
    }

    /// <summary>
    /// Validates that a complete file path pattern has tokens only in allowed locations.
    /// For URLs: Tokens not allowed in protocol://server portion
    /// For paths: Tokens not allowed in server name (for FTP)
    /// </summary>
    /// <param name="serverName">Server name or host</param>
    /// <param name="filePathPattern">File path pattern</param>
    /// <param name="filenamePattern">Filename pattern</param>
    /// <returns>Validation result with success flag and error message</returns>
    public ValidationResult ValidatePatterns(string serverName, string filePathPattern, string filenamePattern)
    {
        // Check server name doesn't contain tokens
        if (!string.IsNullOrWhiteSpace(serverName) && ContainsTokens(serverName))
        {
            return new ValidationResult(false, "Server name/host cannot contain date tokens like {yyyy}, {mm}, {dd}");
        }

        // File path and filename can contain tokens - no restriction
        // Just validate the tokens themselves are valid
        if (!string.IsNullOrWhiteSpace(filePathPattern))
        {
            var invalidTokens = GetInvalidTokens(filePathPattern);
            if (invalidTokens.Any())
            {
                return new ValidationResult(false, $"File path pattern contains invalid tokens: {string.Join(", ", invalidTokens)}");
            }
        }

        if (!string.IsNullOrWhiteSpace(filenamePattern))
        {
            var invalidTokens = GetInvalidTokens(filenamePattern);
            if (invalidTokens.Any())
            {
                return new ValidationResult(false, $"Filename pattern contains invalid tokens: {string.Join(", ", invalidTokens)}");
            }
        }

        return new ValidationResult(true, null);
    }

    /// <summary>
    /// Checks if a pattern contains any date tokens.
    /// </summary>
    public bool ContainsTokens(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        return TokenPattern.IsMatch(pattern);
    }

    /// <summary>
    /// Gets list of invalid tokens (tokens that don't match the supported format).
    /// </summary>
    private IEnumerable<string> GetInvalidTokens(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return Enumerable.Empty<string>();
        }

        // Find all potential tokens (anything in curly braces)
        var allTokens = Regex.Matches(pattern, @"\{[^}]+\}").Select(m => m.Value).ToHashSet();
        
        // Return tokens that are NOT in the valid list
        return allTokens.Where(t => !ValidTokens.Contains(t, StringComparer.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Result of token validation.
/// </summary>
public record ValidationResult(bool IsValid, string? ErrorMessage);
