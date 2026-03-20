using NServiceBus;

namespace FileProcessing.Contracts.Events;

/// <summary>
/// Event published when a file is successfully retrieved and stored to blob storage.
/// Published by RetrieveFileHandler after downloading and storing the file.
/// Subscribers: Processing systems for parsing and further operations.
/// </summary>
public record FileRetrieved : IEvent
{
    public Guid MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset OccurredUtc { get; init; }
    public required string IdempotencyKey { get; init; }
    
    // File Processing context
    public required string ClientId { get; init; }
    public required Guid ConfigurationId { get; init; }
    
    // File details
    public required string FileName { get; init; }
    public required string BlobStorageUrl { get; init; }
    
    // Execution tracking
    public required Guid ExecutionId { get; init; }
    public required Guid DiscoveredFileId { get; init; }
    public required string FileUrl { get; init; }
    public required string Protocol { get; init; }
}
