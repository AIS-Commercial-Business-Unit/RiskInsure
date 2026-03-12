using FileRetrieval.Contracts.DTOs;
using NServiceBus;

namespace FileRetrieval.Contracts.Commands;

/// <summary>
/// Command to process a parsed NACHA entry row.
/// </summary>
public record ProcessNachaRow : ICommand
{
    public Guid MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset OccurredUtc { get; init; }
    public required string IdempotencyKey { get; init; }

    public required string ClientId { get; init; }
    public Guid ConfigurationId { get; init; }
    public Guid ExecutionId { get; init; }
    public Guid DiscoveredFileId { get; init; }
    public required string Filename { get; init; }
    public required NachaRow Row { get; init; }
}