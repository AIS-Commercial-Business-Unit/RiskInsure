namespace RiskInsure.Modernization.Chat.Services;

using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;

public interface ISearchService
{
    Task<List<SearchResultItem>> SearchPatternsAsync(
        string query,
        float[]? embeddingVector = null,
        int topK = 5,
        CancellationToken cancellationToken = default);
}

public class SearchService : ISearchService
{
    private readonly SearchClient _searchClient;
    private readonly ILogger<SearchService> _logger;

    public SearchService(IConfiguration config, ILogger<SearchService> logger)
    {
        _logger = logger;

        var endpoint = config["AzureSearch:Endpoint"]
            ?? throw new InvalidOperationException("AzureSearch:Endpoint not configured");
        var apiKey = config["AzureSearch:ApiKey"]
            ?? throw new InvalidOperationException("AzureSearch:ApiKey not configured");
        var indexName = config["AzureSearch:IndexName"] ?? "modernization-patterns";

        var credential = new AzureKeyCredential(apiKey);
        _searchClient = new SearchClient(new Uri(endpoint), indexName, credential);
    }

    public async Task<List<SearchResultItem>> SearchPatternsAsync(
        string query,
        float[]? embeddingVector = null,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching patterns for: {Query}", query);

        var searchOptions = new SearchOptions
        {
            Size = topK,
            IncludeTotalCount = true
        };

        var results = await _searchClient.SearchAsync<SearchDocument>(query, searchOptions, cancellationToken);

        var resultList = new List<SearchResultItem>();
        await foreach (var result in results.Value.GetResultsAsync())
        {
            var doc = result.Document;
            resultList.Add(new SearchResultItem
            {
                Id = doc["id"]?.ToString() ?? "",
                PatternSlug = doc["patternSlug"]?.ToString() ?? "",
                Title = doc["title"]?.ToString() ?? "",
                Category = doc["category"]?.ToString() ?? "",
                Content = doc["content"]?.ToString() ?? "",
                Relevance = result.Score ?? 0
            });

            _logger.LogDebug("Found: {Title} (score: {Score})",
                doc["title"], result.Score);
        }

        return resultList;
    }
}

public class SearchResultItem
{
    public required string Id { get; init; }
    public required string PatternSlug { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string Content { get; init; }
    public required double Relevance { get; init; }
}
