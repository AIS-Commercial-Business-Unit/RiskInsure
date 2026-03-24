namespace RiskInsure.Policy.Domain.Contracts.Events;

public record PolicyBound(
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
