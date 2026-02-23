using NServiceBus;

namespace FileRetrieval.Contracts.Events;

/// <summary>
/// Event published when a file matching a FileRetrievalConfiguration is discovered.
/// Subscribers: Workflow orchestration platform, monitoring systems, audit logs.
/// </summary>
public record FileDiscovered : IEvent
{
    public Guid MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset OccurredUtc { get; init; }
    public required string IdempotencyKey { get; init; }
    
    // File details
    public required string ClientId { get; init; }
    public Guid ConfigurationId { get; init; }
    public Guid ExecutionId { get; init; }
    public Guid DiscoveredFileId { get; init; }
    public required string FileUrl { get; init; }
    public required string Filename { get; init; }
    public long? FileSize { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public DateTimeOffset DiscoveredAt { get; init; }
    
    // Configuration metadata
    public required string ConfigurationName { get; init; }
    public required string Protocol { get; init; } // "Ftp", "Https", "AzureBlob"
    
    // Custom event data from EventDefinition
    public Dictionary<string, object>? EventData { get; init; }
}
