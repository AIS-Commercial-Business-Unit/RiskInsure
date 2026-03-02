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
    private readonly SecretClient _keyVaultClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpsProtocolAdapter> _logger;
    private string? _cachedSecret;

    public string ProtocolType => "HTTPS";

    public HttpsProtocolAdapter(
        HttpsProtocolSettings settings,
        SecretClient keyVaultClient,
        IHttpClientFactory httpClientFactory,
        ILogger<HttpsProtocolAdapter> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _keyVaultClient = keyVaultClient ?? throw new ArgumentNullException(nameof(keyVaultClient));
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

            // Build full URL (combine base URL with path pattern)
            var fullUrl = CombineUrl(_settings.BaseUrl, filePathPattern);

            // For HTTPS, we assume the endpoint returns a JSON array of file metadata
            // or a directory listing in a standard format
            // This is a simplified implementation - real-world might need content negotiation
            
            _logger.LogDebug("Sending GET request to {Url}", fullUrl);
            
            var response = await httpClient.GetAsync(fullUrl, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "HTTPS request failed with status {StatusCode}: {ReasonPhrase}",
                    response.StatusCode,
                    response.ReasonPhrase);
                throw new HttpRequestException(
                    $"HTTP request failed with status {response.StatusCode}: {response.ReasonPhrase}");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            
            // Handle JSON response (assuming array of file objects)
            if (contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var files = JsonSerializer.Deserialize<List<HttpFileEntry>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (files != null)
                {
                    foreach (var file in files)
                    {
                        // Check if filename matches pattern
                        if (!MatchesPattern(file.Name ?? string.Empty, filenamePattern))
                        {
                            continue;
                        }

                        // Check file extension if specified
                        if (!string.IsNullOrWhiteSpace(fileExtension))
                        {
                            var itemExtension = Path.GetExtension(file.Name ?? string.Empty).TrimStart('.');
                            if (!itemExtension.Equals(fileExtension, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }

                        var fileUrl = file.Url ?? CombineUrl(fullUrl, file.Name ?? string.Empty);

                        var discoveredFile = new DiscoveredFileInfo
                        {
                            FileUrl = fileUrl,
                            Filename = file.Name ?? Path.GetFileName(fileUrl),
                            FileSize = file.Size > 0 ? file.Size : null,
                            LastModified = file.LastModified,
                            DiscoveredAt = DateTimeOffset.UtcNow,
                            ProtocolMetadata = new Dictionary<string, object>
                            {
                                ["ContentType"] = file.ContentType ?? "unknown",
                                ["ETag"] = file.ETag ?? string.Empty
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
            }
            else
            {
                // For non-JSON responses, assume single file at the URL
                // Check if filename matches pattern
                var filename = ExtractFilenameFromUrl(fullUrl);
                
                if (MatchesPattern(filename, filenamePattern))
                {
                    // Check file extension
                    if (string.IsNullOrWhiteSpace(fileExtension) ||
                        Path.GetExtension(filename).TrimStart('.').Equals(fileExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        var discoveredFile = new DiscoveredFileInfo
                        {
                            FileUrl = fullUrl,
                            Filename = filename,
                            FileSize = response.Content.Headers.ContentLength,
                            LastModified = response.Content.Headers.LastModified,
                            DiscoveredAt = DateTimeOffset.UtcNow,
                            ProtocolMetadata = new Dictionary<string, object>
                            {
                                ["ContentType"] = contentType ?? "unknown",
                                ["ETag"] = response.Headers.ETag?.Tag ?? string.Empty
                            }
                        };

                        discoveredFiles.Add(discoveredFile);
                    }
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
                if (!string.IsNullOrWhiteSpace(_settings.UsernameOrApiKey) &&
                    !string.IsNullOrWhiteSpace(_settings.PasswordOrTokenKeyVaultSecret))
                {
                    var password = await GetSecretAsync(cancellationToken);
                    var credentials = Convert.ToBase64String(
                        System.Text.Encoding.ASCII.GetBytes($"{_settings.UsernameOrApiKey}:{password}"));
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new AuthenticationHeaderValue("Basic", credentials);
                }
                break;

            case AuthType.BearerToken:
                if (!string.IsNullOrWhiteSpace(_settings.PasswordOrTokenKeyVaultSecret))
                {
                    var token = await GetSecretAsync(cancellationToken);
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new AuthenticationHeaderValue("Bearer", token);
                }
                break;

            case AuthType.ApiKey:
                if (!string.IsNullOrWhiteSpace(_settings.UsernameOrApiKey))
                {
                    // Add API key to header (common patterns: X-API-Key, api-key, apikey)
                    httpClient.DefaultRequestHeaders.Add("X-API-Key", _settings.UsernameOrApiKey);
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
    /// Retrieves secret from Key Vault with caching.
    /// </summary>
    private async Task<string> GetSecretAsync(CancellationToken cancellationToken)
    {
        if (_cachedSecret == null)
        {
            _cachedSecret = _settings.PasswordOrTokenKeyVaultSecret;

            // Todo: Implement proper secret retrieval with error handling and caching            
            //     var secret = await _keyVaultClient.GetSecretAsync(
            //         _settings.PasswordOrTokenKeyVaultSecret,
            //         cancellationToken: cancellationToken);
            // _cachedSecret = secret.Value.Value;
        }

        if (string.IsNullOrWhiteSpace(_cachedSecret))
        {
            throw new InvalidOperationException(
                "Could not retrieve secret for HttpsProtocolAdapter from PasswordOrTokenKeyVaultSecret.  PasswordOrTokenKeyVaultSecret must be configured for this authentication type.");
        }

        return _cachedSecret;
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

    /// <summary>
    /// DTO for JSON file listing responses.
    /// </summary>
    private class HttpFileEntry
    {
        public string? Name { get; set; }
        public string? Url { get; set; }
        public long Size { get; set; }
        public DateTimeOffset? LastModified { get; set; }
        public string? ContentType { get; set; }
        public string? ETag { get; set; }
    }
}
