namespace RiskInsure.PublicContracts.Events;

/// <summary>
/// Event published when a new order is placed in the Sales system
/// </summary>
public record OrderPlaced(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid OrderId,
    string IdempotencyKey
);
