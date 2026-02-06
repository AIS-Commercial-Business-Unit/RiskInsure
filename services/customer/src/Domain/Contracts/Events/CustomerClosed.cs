namespace RiskInsure.Customer.Domain.Contracts.Events;

public record CustomerClosed(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string CustomerId,
    string IdempotencyKey
);
