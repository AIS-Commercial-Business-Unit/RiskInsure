namespace RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Contracts.Events;

using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Models;

/// <summary>
/// Event published when a billing cycle is updated
/// </summary>
public record BillingCycleUpdated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string AccountId,
    BillingCycle OldBillingCycle,
    BillingCycle NewBillingCycle,
    string ChangeReason,
    string IdempotencyKey
);
