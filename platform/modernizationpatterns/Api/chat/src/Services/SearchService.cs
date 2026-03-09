namespace RiskInsure.Modernization.Chat.Services;

using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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
    private static readonly HttpClient HttpClient = new();
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _indexName;
    private readonly ILogger<SearchService> _logger;

    public SearchService(IConfiguration config, ILogger<SearchService> logger)
    {
        _logger = logger;

        var endpoint = config["AzureSearch:Endpoint"];
        var apiKey = config["AzureSearch:ApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogError("AzureSearch:Endpoint configuration is missing or empty");
            throw new InvalidOperationException("AzureSearch:Endpoint not configured");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("AzureSearch:ApiKey configuration is missing or empty");
            throw new InvalidOperationException("AzureSearch:ApiKey not configured");
        }

        _endpoint = endpoint.TrimEnd('/');
        _apiKey = apiKey;
        _indexName = config["AzureSearch:IndexName"] ?? "modernization-patterns";

        _logger.LogInformation(
            "SearchService initialized with endpoint: {Endpoint}, index: {IndexName}",
            _endpoint, 
            _indexName);
    }

    public async Task<List<SearchResultItem>> SearchPatternsAsync(
        string query,
        float[]? embeddingVector = null,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching patterns for: {Query}", query);

        try
        {
            var url = $"{_endpoint}/indexes/{_indexName}/docs/search?api-version=2024-05-01-preview";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("api-key", _apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var searchPayload = new
            {
                search = query,
                top = topK,
                count = true,
                select = "id,patternSlug,title,category,content"
            };

            var payload = JsonSerializer.Serialize(searchPayload);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Search failed with {StatusCode}: {Response}", response.StatusCode, content);
                throw new InvalidOperationException(
                    $"Search request failed: {(int)response.StatusCode} {response.ReasonPhrase}. Response: {content}");
            }

            using var document = JsonDocument.Parse(content);
            var resultList = new List<SearchResultItem>();

            if (document.RootElement.TryGetProperty("value", out var valueArray))
            {
                foreach (var result in valueArray.EnumerateArray())
                {
                    resultList.Add(new SearchResultItem
                    {
                        Id = result.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        PatternSlug = result.TryGetProperty("patternSlug", out var slug) ? slug.GetString() ?? "" : "",
                        Title = result.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                        Category = result.TryGetProperty("category", out var cat) ? cat.GetString() ?? "" : "",
                        Content = result.TryGetProperty("content", out var cont) ? cont.GetString() ?? "" : "",
                        Relevance = result.TryGetProperty("@search.score", out var score) ? score.GetDouble() : 0
                    });

                    _logger.LogDebug("Found: {Title}",
                        result.TryGetProperty("title", out var t) ? t.GetString() ?? "unknown" : "unknown");
                }
            }

            return resultList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for query: {Query}", query);
            throw;
        }
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
