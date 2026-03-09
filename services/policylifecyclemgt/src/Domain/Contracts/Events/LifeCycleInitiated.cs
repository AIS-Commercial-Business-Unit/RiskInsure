namespace RiskInsure.PolicyLifeCycleMgt.Domain.Contracts.Events;

public record LifeCycleInitiated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyId,
    string PolicyNumber,
    string QuoteId,
    string CustomerId,
    decimal Premium,
    DateTimeOffset EffectiveDate,
    DateTimeOffset ExpirationDate,
    string IdempotencyKey
);
