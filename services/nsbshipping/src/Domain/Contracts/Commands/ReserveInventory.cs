namespace RiskInsure.NsbShipping.Domain.Contracts.Commands;

public record ReserveInventory(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid OrderId,
    string IdempotencyKey
);
