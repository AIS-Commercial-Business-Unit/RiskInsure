namespace RiskInsure.Billing.Domain.Contracts.Events;

using RiskInsure.Billing.Domain.Models;

/// <summary>
/// Event published when a new billing account is created
/// </summary>
public record BillingAccountCreated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string AccountId,
    string CustomerId,
    string PolicyNumber,
    string PolicyHolderName,
    decimal CurrentPremiumOwed,
    BillingCycle BillingCycle,
    DateTimeOffset EffectiveDate,
    string IdempotencyKey
);
