namespace RiskInsure.NsbShipping.Domain.Contracts.Events;

public record InventoryReserved(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid OrderId,
    string IdempotencyKey
);
