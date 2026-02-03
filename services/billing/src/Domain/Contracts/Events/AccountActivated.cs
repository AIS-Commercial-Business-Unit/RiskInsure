namespace RiskInsure.Billing.Domain.Contracts.Events;

/// <summary>
/// Event published when a billing account is activated
/// </summary>
public record AccountActivated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string AccountId,
    string PolicyNumber,
    string IdempotencyKey
);
