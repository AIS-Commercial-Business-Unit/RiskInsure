namespace RiskInsure.Policy.Domain.Contracts.Events;

public record PolicyRenewalWindowOpened(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyId,
    string PolicyTermId,
    DateTimeOffset RenewalWindowStartUtc,
    string IdempotencyKey
);
