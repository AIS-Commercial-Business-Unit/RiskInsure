namespace RiskInsure.Policy.Domain.Contracts.Events;

public record PolicyRenewalAccepted(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyId,
    string CurrentPolicyTermId,
    string NextPolicyTermId,
    DateTimeOffset NextTermEffectiveDate,
    DateTimeOffset NextTermExpirationDate,
    string IdempotencyKey
);
