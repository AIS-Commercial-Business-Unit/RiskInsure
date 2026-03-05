using NServiceBus;

namespace FileRetrieval.Contracts.Events;

/// <summary>
/// Event published when a scheduled file check fails after retry attempts.
/// Subscribers: Monitoring systems, operational alerts, incident management.
/// </summary>
public record FileCheckFailed : IEvent
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
    public DateTimeOffset ExecutionFailedAt { get; init; }
    public long DurationMs { get; init; }
    public int RetryCount { get; init; }
    
    // Error details
    public required string ErrorMessage { get; init; }
    public required string ErrorCategory { get; init; } // "AuthenticationFailure", "ConnectionTimeout", etc.
    public string? StackTrace { get; init; } // Optional, for debugging
    
    // Resolved patterns (after token replacement)
    public required string ResolvedFilePathPattern { get; init; }
    public required string ResolvedFilenamePattern { get; init; }
}
