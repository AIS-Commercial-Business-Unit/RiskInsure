namespace RiskInsure.FileRetrieval.API.Models;

/// <summary>
/// Response model for paginated configuration list.
/// Supports US4 requirement for clients with 20+ configurations.
/// </summary>
public class PaginatedConfigurationResponse
{
    /// <summary>
    /// List of configurations for current page
    /// </summary>
    public required List<ConfigurationResponse> Configurations { get; init; }

    /// <summary>
    /// Continuation token for next page (null if no more pages)
    /// </summary>
    public string? ContinuationToken { get; init; }

    /// <summary>
    /// Whether there are more pages available
    /// </summary>
    public bool HasMore { get; init; }

    /// <summary>
    /// Number of configurations in current page
    /// </summary>
    public int Count { get; init; }
}
