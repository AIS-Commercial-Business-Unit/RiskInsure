using NServiceBus;

namespace FileRetrieval.Contracts.Events;

/// <summary>
/// Event published when a FileRetrievalConfiguration is updated.
/// Subscribers: Audit log, monitoring systems, scheduler (to refresh cache).
/// </summary>
public record ConfigurationUpdated : IEvent
{
    public Guid MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset OccurredUtc { get; init; }
    public required string IdempotencyKey { get; init; }
    
    // Configuration details
    public required string ClientId { get; init; }
    public Guid ConfigurationId { get; init; }
    public required string Name { get; init; }
    public required string Protocol { get; init; }
    public required string FilePathPattern { get; init; }
    public required string FilenamePattern { get; init; }
    public required string CronExpression { get; init; }
    public required string Timezone { get; init; }
    public bool IsActive { get; init; }
    public required string LastModifiedBy { get; init; }
    
    // Change tracking
    public required List<string> ChangedFields { get; init; } // List of field names that changed
}
