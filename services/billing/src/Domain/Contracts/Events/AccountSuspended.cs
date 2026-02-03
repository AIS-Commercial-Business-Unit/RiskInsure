namespace RiskInsure.Billing.Domain.Contracts.Events;

/// <summary>
/// Event published when a billing account is suspended
/// </summary>
public record AccountSuspended(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string AccountId,
    string PolicyNumber,
    string SuspensionReason,
    string IdempotencyKey
);
