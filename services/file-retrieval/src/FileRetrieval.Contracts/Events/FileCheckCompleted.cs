using NServiceBus;

namespace FileRetrieval.Contracts.Events;

/// <summary>
/// Event published when a scheduled file check completes successfully.
/// Subscribers: Monitoring systems, dashboards, operational alerts.
/// </summary>
public record FileCheckCompleted : IEvent
{
    public Guid MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset OccurredUtc { get; init; }
    public required string IdempotencyKey { get; init; }
    
    // Execution details
    public required string ClientId { get; init; }
    public Guid ConfigurationId { get; init; }
    public Guid ExecutionId { get; init; }
    public required string ConfigurationName { get; init; }
    public required string Protocol { get; init; }
    
    // Execution results
    public DateTimeOffset ExecutionStartedAt { get; init; }
    public DateTimeOffset ExecutionCompletedAt { get; init; }
    public int FilesFound { get; init; }
    public int FilesProcessed { get; init; }
    public long DurationMs { get; init; }
    
    // Resolved patterns (after token replacement)
    public required string ResolvedFilePathPattern { get; init; }
    public required string ResolvedFilenamePattern { get; init; }
}
