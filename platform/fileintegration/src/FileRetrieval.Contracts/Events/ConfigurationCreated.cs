using NServiceBus;

namespace FileRetrieval.Contracts.Events;

/// <summary>
/// Event published when a new FileRetrievalConfiguration is created.
/// Subscribers: Audit log, monitoring systems, scheduler (to refresh cache).
/// </summary>
public record ConfigurationCreated : IEvent
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
    public required string CreatedBy { get; init; }
    
    // Metadata
    public int EventCount { get; init; } // Number of events to publish
}
