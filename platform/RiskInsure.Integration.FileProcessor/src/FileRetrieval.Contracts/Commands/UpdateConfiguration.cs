using NServiceBus;
using FileRetrieval.Contracts.DTOs;

namespace FileRetrieval.Contracts.Commands;

/// <summary>
/// Command to update an existing FileRetrievalConfiguration.
/// Sent by API Controller, handled by UpdateConfigurationHandler.
/// </summary>
public record UpdateConfiguration : ICommand
{
    public Guid MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset OccurredUtc { get; init; }
    public required string IdempotencyKey { get; init; }
    
    // Configuration details
    public required string ClientId { get; init; }
    public Guid ConfigurationId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Protocol { get; init; }
    public required Dictionary<string, object> ProtocolSettings { get; init; }
    public required string FilePathPattern { get; init; }
    public required string FilenamePattern { get; init; }
    public string? FileExtension { get; init; }
    public required ScheduleDefinitionDto Schedule { get; init; }
    public bool IsActive { get; init; }
    public required string LastModifiedBy { get; init; } // User ID from JWT claims
    public required string ETag { get; init; } // Optimistic concurrency
}
