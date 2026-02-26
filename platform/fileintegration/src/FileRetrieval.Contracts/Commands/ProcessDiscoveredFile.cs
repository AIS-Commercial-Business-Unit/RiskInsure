using NServiceBus;

namespace FileRetrieval.Contracts.Commands;

/// <summary>
/// Command sent to the workflow orchestration platform to process a discovered file.
/// Sent by FileCheckService when a file matching a configuration is found.
/// </summary>
public record ProcessDiscoveredFile : ICommand
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
    public required string Protocol { get; init; }
    
    // Custom command data from CommandDefinition
    public Dictionary<string, object>? CommandData { get; init; }
}
