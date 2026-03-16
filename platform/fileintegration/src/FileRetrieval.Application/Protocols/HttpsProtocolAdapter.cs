using RiskInsure.FileRetrieval.Domain.ValueObjects;
using RiskInsure.FileRetrieval.Domain.Enums;
using Microsoft.Extensions.Logging;
using Azure.Security.KeyVault.Secrets;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FileRetrieval.Application.Protocols;

/// <summary>
/// Protocol adapter for HTTPS file retrieval.
/// Supports various authentication methods: None, BasicAuth, BearerToken, ApiKey.
/// Uses HttpClient with IHttpClientFactory for connection pooling.
/// </summary>
public class HttpsProtocolAdapter : IProtocolAdapter
{
    private readonly HttpsProtocolSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpsProtocolAdapter> _logger;

    public string ProtocolType => "HTTPS";

    public HttpsProtocolAdapter(
        HttpsProtocolSettings settings,
        IHttpClientFactory httpClientFactory,
        ILogger<HttpsProtocolAdapter> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DiscoveredFileInfo>> CheckForFilesAsync(
        string serverAddress,
        string filePathPattern,
        string filenamePattern,
        string? fileExtension,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Starting HTTPS file check on {BaseUrl} - Path: {Path}, Pattern: {Pattern}",
            _settings.BaseUrl,
            filePathPattern,
            filenamePattern);

        var discoveredFiles = new List<DiscoveredFileInfo>();

        try
        {
            using var httpClient = await CreateConfiguredHttpClientAsync(cancellationToken);

            // For HTTPS, we assume the endpoint returns 
            // a directory listing in a standard format
            // This is a simplified implementation - real-world might need content negotiation
            
            _logger.LogInformation("Sending GET request to {Url}", _settings.BaseUrl);
            
            var response = await httpClient.GetAsync(_settings.BaseUrl, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "HTTPS request failed with status {StatusCode}: {ReasonPhrase}",
                    response.StatusCode,
                    response.ReasonPhrase);
                throw new HttpRequestException(
                    $"HTTP request failed with status {response.StatusCode}: {response.ReasonPhrase}");
            }            

            var contentType = response.Content.Headers.ContentType?.MediaType;
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var filesFound = NginxDirectoryListingParser.ParseNginxFileList(content, _settings.BaseUrl);

            if (filesFound != null)
            {
                foreach (var file in filesFound)
                {
                    // Check if filename matches pattern
                    if (!MatchesPattern(file.Name ?? string.Empty, filenamePattern))
                    {
                        _logger.LogDebug(
                            "Skipping file {Filename} as it does not match pattern {Pattern}",
                            file.Name,
                            filenamePattern);
                        continue;
                    }

                    // Check file extension if specified
                    if (!string.IsNullOrWhiteSpace(fileExtension))
                    {
                        var itemExtension = Path.GetExtension(file.Name ?? string.Empty).TrimStart('.');
                        if (!itemExtension.Equals(fileExtension, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogDebug(
                                "Skipping file {Filename} as it does not match extension {Extension}",
                                file.Name,
                                fileExtension);
                            continue;
                        }
                    }

                    var discoveredFile = new DiscoveredFileInfo
                    {
                        FileUrl = file.Url,
                        Filename = file.Name ?? Path.GetFileName(file.Url),
                        FileSize = file.Size > 0 ? file.Size : null,
                        LastModified = file.Date,
                        DiscoveredAt = DateTimeOffset.UtcNow,
                        ProtocolMetadata = new Dictionary<string, object>
                        {
                            ["ContentType"] = "unknown",
                            ["ETag"] = string.Empty
                        }
                    };

                    discoveredFiles.Add(discoveredFile);

                    _logger.LogDebug(
                        "Discovered HTTPS file: {Filename} ({Size} bytes) at {Url}",
                        discoveredFile.Filename,
                        discoveredFile.FileSize,
                        discoveredFile.FileUrl);
                }
            }

            _logger.LogInformation(
                "HTTPS file check completed: {Count} files discovered at {BaseUrl}",
                discoveredFiles.Count,
                _settings.BaseUrl);

            return discoveredFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "HTTPS file check failed on {BaseUrl} - Path: {Path}",
                _settings.BaseUrl,
                filePathPattern);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Testing HTTPS connection to {BaseUrl}", _settings.BaseUrl);

            using var httpClient = await CreateConfiguredHttpClientAsync(cancellationToken);

            // Send HEAD request to base URL to test connectivity
            var response = await httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, _settings.BaseUrl),
                cancellationToken);

            var isSuccess = response.IsSuccessStatusCode;

            _logger.LogInformation(
                "HTTPS connection test {Result} to {BaseUrl} (Status: {StatusCode})",
                isSuccess ? "successful" : "failed",
                _settings.BaseUrl,
                response.StatusCode);

            return isSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HTTPS connection test failed to {BaseUrl}", _settings.BaseUrl);
            return false;
        }
    }

    /// <summary>
    /// Creates HttpClient configured with authentication and settings.
    /// </summary>
    private async Task<HttpClient> CreateConfiguredHttpClientAsync(CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("FileRetrievalHttps");
        httpClient.Timeout = _settings.ConnectionTimeout;
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Configure authentication
        switch (_settings.AuthenticationType)
        {
            case AuthType.UsernamePassword:
                if (!string.IsNullOrWhiteSpace(_settings.Username) &&
                    !string.IsNullOrWhiteSpace(_settings.PasswordOrTokenOrApiKey))
                {
                    var credentials = Convert.ToBase64String(
                        System.Text.Encoding.ASCII.GetBytes($"{_settings.Username}:{_settings.PasswordOrTokenOrApiKey}"));
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new AuthenticationHeaderValue("Basic", credentials);
                }
                break;

            case AuthType.BearerToken:
                if (!string.IsNullOrWhiteSpace(_settings.PasswordOrTokenOrApiKey))
                {
                    var token = _settings.PasswordOrTokenOrApiKey;
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new AuthenticationHeaderValue("Bearer", token);
                }
                break;

            case AuthType.ApiKey:
                if (!string.IsNullOrWhiteSpace(_settings.PasswordOrTokenOrApiKey))
                {
                    // Add API key to header (common patterns: X-API-Key, api-key, apikey)
                    httpClient.DefaultRequestHeaders.Add("X-API-Key", _settings.PasswordOrTokenOrApiKey);
                }
                break;

            case AuthType.None:
            default:
                // No authentication required
                break;
        }

        return httpClient;
    }



    /// <summary>
    /// Combines base URL with path, handling trailing/leading slashes.
    /// </summary>
    private string CombineUrl(string baseUrl, string path)
    {
        var trimmedBase = baseUrl.TrimEnd('/');
        var trimmedPath = path.TrimStart('/');
        return $"{trimmedBase}/{trimmedPath}";
    }

    /// <summary>
    /// Extracts filename from URL path.
    /// </summary>
    private string ExtractFilenameFromUrl(string url)
    {
        var uri = new Uri(url);
        return Path.GetFileName(uri.LocalPath);
    }

    /// <summary>
    /// Matches filename against pattern with wildcard support.
    /// </summary>
    private bool MatchesPattern(string filename, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern == "*")
        {
            return true;
        }

        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            filename,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
