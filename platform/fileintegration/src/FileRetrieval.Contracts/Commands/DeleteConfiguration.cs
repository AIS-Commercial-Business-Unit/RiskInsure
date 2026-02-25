using NServiceBus;

namespace FileRetrieval.Contracts.Commands;

/// <summary>
/// Command to delete (soft-delete) a FileRetrievalConfiguration.
/// Sent by API Controller, handled by DeleteConfigurationHandler.
/// Sets IsActive = false to preserve execution history.
/// </summary>
public record DeleteConfiguration : ICommand
{
    public Guid MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset OccurredUtc { get; init; }
    public required string IdempotencyKey { get; init; }
    
    // Configuration details
    public required string ClientId { get; init; }
    public Guid ConfigurationId { get; init; }
    public required string DeletedBy { get; init; } // User ID from JWT claims
    public required string ETag { get; init; } // Optimistic concurrency
}
