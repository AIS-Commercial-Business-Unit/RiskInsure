namespace RiskInsure.NsbShipping.Domain.Contracts.Events;

public record OrderShipped(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid OrderId,
    string IdempotencyKey
);
