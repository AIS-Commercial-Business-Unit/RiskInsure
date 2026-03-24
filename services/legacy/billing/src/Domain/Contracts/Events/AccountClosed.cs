namespace RiskInsure.Billing.Domain.Contracts.Events;

/// <summary>
/// Event published when a billing account is closed
/// </summary>
public record AccountClosed(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string AccountId,
    string PolicyNumber,
    string ClosureReason,
    decimal FinalOutstandingBalance,
    string IdempotencyKey
);
