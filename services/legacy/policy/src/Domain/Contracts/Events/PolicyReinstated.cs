namespace RiskInsure.Policy.Domain.Contracts.Events;

public record PolicyReinstated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyId,
    string PolicyNumber,
    string CustomerId,
    decimal PaymentAmount,
    string IdempotencyKey
);
