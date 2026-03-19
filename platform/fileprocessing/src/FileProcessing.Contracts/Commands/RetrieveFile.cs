using NServiceBus;

namespace FileProcessing.Contracts.Commands;

/// <summary>
/// Command to trigger a file check for a specific FileProcessingConfiguration.
/// Sent by SchedulerHostedService when a schedule fires, or manually triggered via API.
/// </summary>
public record RetrieveFile : ICommand
{
    public Guid MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset OccurredUtc { get; init; }
    public required string IdempotencyKey { get; init; }
    
    // File Processing specific
    public required string ClientId { get; init; }
    public Guid ConfigurationId { get; init; }
    public DateTimeOffset ScheduledExecutionTime { get; init; }
    
    /// <summary>
    /// False = scheduled execution, True = manual trigger from API
    /// </summary>
    public bool IsManualTrigger { get; init; }
}
