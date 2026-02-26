namespace FileRetrieval.Application.Protocols;

/// <summary>
/// Represents metadata about a discovered file from a protocol adapter.
/// </summary>
public record DiscoveredFileInfo
{
    /// <summary>
    /// Full URL or path to the discovered file.
    /// Format depends on protocol:
    /// - FTP: ftp://server.com/path/to/file.xlsx
    /// - HTTPS: https://server.com/path/to/file.xlsx
    /// - Azure Blob: https://account.blob.core.windows.net/container/path/to/file.xlsx
    /// </summary>
    public required string FileUrl { get; init; }

    /// <summary>
    /// Filename only (without path).
    /// Example: "transactions_20250124.xlsx"
    /// </summary>
    public required string Filename { get; init; }

    /// <summary>
    /// File size in bytes (if available from protocol).
    /// Null if protocol doesn't provide size information.
    /// </summary>
    public long? FileSize { get; init; }

    /// <summary>
    /// Last modified timestamp (if available from protocol).
    /// Null if protocol doesn't provide modification time.
    /// </summary>
    public DateTimeOffset? LastModified { get; init; }

    /// <summary>
    /// When this file was discovered by the protocol adapter.
    /// </summary>
    public DateTimeOffset DiscoveredAt { get; init; }

    /// <summary>
    /// Protocol-specific metadata (e.g., ETag for Azure Blob, content type, etc.).
    /// Optional additional information that might be useful for downstream processing.
    /// </summary>
    public Dictionary<string, object>? ProtocolMetadata { get; init; }
}
