namespace RiskInsure.PublicContracts.Events;

/// <summary>
/// Event published when an order has been billed by the Billing system
/// </summary>
public record OrderBilled(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid OrderId,
    string IdempotencyKey
);
