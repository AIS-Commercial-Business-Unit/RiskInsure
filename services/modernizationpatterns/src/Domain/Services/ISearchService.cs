namespace RiskInsure.ModernizationPatternsMgt.Domain.Services;

using RiskInsure.ModernizationPatternsMgt.Domain.Models;

/// <summary>
/// Azure AI Search service for semantic pattern search
/// </summary>
public interface ISearchService
{
    Task<List<SearchResultItem>> SearchPatternsAsync(
        string query,
        float[]? embeddingVector = null,
        int topK = 5,
        CancellationToken cancellationToken = default);
}
