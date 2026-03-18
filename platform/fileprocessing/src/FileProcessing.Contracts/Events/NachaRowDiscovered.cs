using FileProcessing.Contracts.DTOs;
using NServiceBus;

namespace FileProcessing.Contracts.Events;

/// <summary>
/// Event published when a NACHA row is parsed from a discovered file.
/// </summary>
public record NachaRowDiscovered : IEvent
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