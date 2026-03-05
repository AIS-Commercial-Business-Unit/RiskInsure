namespace FileRetrieval.Application.Protocols;

/// <summary>
/// Interface for protocol-specific file discovery adapters.
/// Each protocol (FTP, HTTPS, Azure Blob) implements this interface.
/// </summary>
public interface IProtocolAdapter
{
    /// <summary>
    /// Checks the specified location for files matching the provided patterns.
    /// </summary>
    /// <param name="serverAddress">Server/host address (already resolved, no tokens)</param>
    /// <param name="filePathPattern">File path pattern (tokens already replaced)</param>
    /// <param name="filenamePattern">Filename pattern (tokens already replaced, may contain wildcards)</param>
    /// <param name="fileExtension">Optional file extension filter (e.g., "xlsx", "pdf")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of discovered files with metadata</returns>
    Task<IEnumerable<DiscoveredFileInfo>> CheckForFilesAsync(
        string serverAddress,
        string filePathPattern,
        string filenamePattern,
        string? fileExtension,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connectivity to the server/service using the protocol-specific settings.
    /// Used for configuration validation and troubleshooting.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection successful, false otherwise</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the protocol type this adapter supports.
    /// </summary>
    string ProtocolType { get; }
}
