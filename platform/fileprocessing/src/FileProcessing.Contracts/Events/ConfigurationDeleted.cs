using NServiceBus;

namespace FileProcessing.Contracts.Events;

/// <summary>
/// Event published when a FileProcessingConfiguration is deleted (soft-deleted).
/// Subscribers: Audit log, monitoring systems, scheduler (to remove from cache).
/// </summary>
public record ConfigurationDeleted : IEvent
{
    public Guid MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset OccurredUtc { get; init; }
    public required string IdempotencyKey { get; init; }
    
    // Configuration details
    public required string ClientId { get; init; }
    public Guid ConfigurationId { get; init; }
    public required string Name { get; init; }
    public required string DeletedBy { get; init; }
    
    // Deletion metadata
    public bool IsSoftDelete { get; init; } // Always true for now (IsActive = false)
}
