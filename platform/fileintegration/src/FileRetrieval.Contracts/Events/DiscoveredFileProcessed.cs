using NServiceBus;

namespace FileRetrieval.Contracts.Events;

/// <summary>
/// Event published after a discovered file has been processed in memory for downstream processing.
/// </summary>
public record DiscoveredFileProcessed : IEvent
{
    public Guid MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset OccurredUtc { get; init; }
    public required string IdempotencyKey { get; init; }

    public required string ClientId { get; init; }
    public Guid ConfigurationId { get; init; }
    public Guid ExecutionId { get; init; }
    public Guid DiscoveredFileId { get; init; }
    public required string FileUrl { get; init; }
    public required string Filename { get; init; }
    public required string Protocol { get; init; }

    public long DownloadedSizeBytes { get; init; }
    public required string ChecksumAlgorithm { get; init; }
    public required string ChecksumHex { get; init; }
}
