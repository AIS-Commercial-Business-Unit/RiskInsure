using NServiceBus;

namespace FileRetrieval.Contracts.Events;

/// <summary>
/// Event published when a file check is triggered (before execution begins).
/// Captures trigger source (scheduled vs manual) and user context for audit trail.
/// Published by ExecuteFileCheckHandler before calling FileCheckService.
/// </summary>
public record FileCheckTriggered : IEvent
{
    // Standard message metadata
    public Guid MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset OccurredUtc { get; init; }
    public required string IdempotencyKey { get; init; }
    
    // File Retrieval context
    public required string ClientId { get; init; }
    public required Guid ConfigurationId { get; init; }
    public required string ConfigurationName { get; init; }
    public required string Protocol { get; init; }
    
    // Execution tracking
    public required Guid ExecutionId { get; init; }
    public required DateTimeOffset ScheduledExecutionTime { get; init; }
    
    // Trigger context
    public required bool IsManualTrigger { get; init; }
    public required string TriggeredBy { get; init; }
}
