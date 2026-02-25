using FluentFTP;
using RiskInsure.FileRetrieval.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Azure.Security.KeyVault.Secrets;

namespace FileRetrieval.Application.Protocols;

/// <summary>
/// Protocol adapter for FTP and FTPS file retrieval.
/// Uses FluentFTP library for robust FTP/FTPS support with TLS and passive mode.
/// </summary>
public class FtpProtocolAdapter : IProtocolAdapter
{
    private readonly FtpProtocolSettings _settings;
    private readonly SecretClient _keyVaultClient;
    private readonly ILogger<FtpProtocolAdapter> _logger;
    private AsyncFtpClient? _ftpClient;
    private string? _cachedPassword;

    public string ProtocolType => "FTP";

    public FtpProtocolAdapter(
        FtpProtocolSettings settings,
        SecretClient keyVaultClient,
        ILogger<FtpProtocolAdapter> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _keyVaultClient = keyVaultClient ?? throw new ArgumentNullException(nameof(keyVaultClient));
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
            "Starting FTP file check on {Server}:{Port} - Path: {Path}, Pattern: {Pattern}",
            _settings.Server,
            _settings.Port,
            filePathPattern,
            filenamePattern);

        var discoveredFiles = new List<DiscoveredFileInfo>();

        try
        {
            await EnsureConnectedAsync(cancellationToken);

            // List files in the resolved path
            var listings = await _ftpClient!.GetListing(filePathPattern, cancellationToken);

            foreach (var item in listings)
            {
                // Skip directories
                if (item.Type == FtpObjectType.Directory)
                {
                    continue;
                }

                // Check if filename matches pattern (using wildcards)
                if (!MatchesPattern(item.Name, filenamePattern))
                {
                    continue;
                }

                // Check file extension if specified
                if (!string.IsNullOrWhiteSpace(fileExtension))
                {
                    var itemExtension = Path.GetExtension(item.Name).TrimStart('.');
                    if (!itemExtension.Equals(fileExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                // Build full FTP URL
                var ftpUrl = $"ftp://{_settings.Server}:{_settings.Port}{item.FullName}";

                var discoveredFile = new DiscoveredFileInfo
                {
                    FileUrl = ftpUrl,
                    Filename = item.Name,
                    FileSize = item.Size > 0 ? item.Size : null,
                    LastModified = item.Modified != DateTime.MinValue 
                        ? new DateTimeOffset(item.Modified, TimeSpan.Zero) 
                        : null,
                    DiscoveredAt = DateTimeOffset.UtcNow,
                    ProtocolMetadata = new Dictionary<string, object>
                    {
                        ["FtpType"] = item.Type.ToString(),
                        ["Permissions"] = item.RawPermissions ?? string.Empty
                    }
                };

                discoveredFiles.Add(discoveredFile);

                _logger.LogDebug(
                    "Discovered FTP file: {Filename} ({Size} bytes) at {Url}",
                    discoveredFile.Filename,
                    discoveredFile.FileSize,
                    discoveredFile.FileUrl);
            }

            _logger.LogInformation(
                "FTP file check completed: {Count} files discovered on {Server}",
                discoveredFiles.Count,
                _settings.Server);

            return discoveredFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "FTP file check failed on {Server}:{Port} - Path: {Path}",
                _settings.Server,
                _settings.Port,
                filePathPattern);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Testing FTP connection to {Server}:{Port}",
                _settings.Server,
                _settings.Port);

            await EnsureConnectedAsync(cancellationToken);

            // Try to get current directory as a simple connectivity test
            var currentDir = await _ftpClient!.GetWorkingDirectory(cancellationToken);

            _logger.LogInformation(
                "FTP connection test successful to {Server}:{Port} (working dir: {Dir})",
                _settings.Server,
                _settings.Port,
                currentDir);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "FTP connection test failed to {Server}:{Port}",
                _settings.Server,
                _settings.Port);
            return false;
        }
    }

    /// <summary>
    /// Ensures FTP client is connected, creating and connecting if necessary.
    /// </summary>
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_ftpClient != null && _ftpClient.IsConnected)
        {
            return;
        }

        // Retrieve password from Key Vault if not cached
        if (_cachedPassword == null)
        {
            var secret = await _keyVaultClient.GetSecretAsync(
                _settings.PasswordKeyVaultSecret,
                cancellationToken: cancellationToken);
            _cachedPassword = secret.Value.Value;
        }

        // Create FTP client with settings
        _ftpClient = new AsyncFtpClient(
            _settings.Server,
            _settings.Username,
            _cachedPassword,
            _settings.Port);

        // Configure encryption and passive mode
        _ftpClient.Config.EncryptionMode = _settings.UseTls 
            ? FtpEncryptionMode.Explicit 
            : FtpEncryptionMode.None;
        
        _ftpClient.Config.DataConnectionType = _settings.UsePassiveMode 
            ? FtpDataConnectionType.AutoPassive 
            : FtpDataConnectionType.AutoActive;

        _ftpClient.Config.ConnectTimeout = (int)_settings.ConnectionTimeout.TotalMilliseconds;
        _ftpClient.Config.ReadTimeout = (int)_settings.ConnectionTimeout.TotalMilliseconds;
        _ftpClient.Config.DataConnectionConnectTimeout = (int)_settings.ConnectionTimeout.TotalMilliseconds;

        // Validate certificates for FTPS (in production, use proper certificate validation)
        _ftpClient.Config.ValidateAnyCertificate = true;

        await _ftpClient.Connect(cancellationToken);

        _logger.LogDebug(
            "FTP client connected to {Server}:{Port} (TLS: {UseTls}, Passive: {UsePassive})",
            _settings.Server,
            _settings.Port,
            _settings.UseTls,
            _settings.UsePassiveMode);
    }

    /// <summary>
    /// Matches filename against pattern with wildcard support.
    /// Supports * (any characters) and ? (single character) wildcards.
    /// </summary>
    private bool MatchesPattern(string filename, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern == "*")
        {
            return true;
        }

        // Convert wildcard pattern to regex
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            filename,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Disposes FTP client connection.
    /// </summary>
    public void Dispose()
    {
        _ftpClient?.Dispose();
        _ftpClient = null;
        _cachedPassword = null;
    }
}
