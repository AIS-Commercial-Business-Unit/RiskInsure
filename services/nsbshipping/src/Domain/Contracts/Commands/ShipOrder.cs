namespace RiskInsure.NsbShipping.Domain.Contracts.Commands;

public record ShipOrder(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid OrderId,
    string IdempotencyKey
);
