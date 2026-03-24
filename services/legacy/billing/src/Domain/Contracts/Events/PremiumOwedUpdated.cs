namespace RiskInsure.Billing.Domain.Contracts.Events;

/// <summary>
/// Event published when premium owed is updated on an account
/// </summary>
public record PremiumOwedUpdated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string AccountId,
    decimal OldPremiumOwed,
    decimal NewPremiumOwed,
    string ChangeReason,
    string IdempotencyKey
);
