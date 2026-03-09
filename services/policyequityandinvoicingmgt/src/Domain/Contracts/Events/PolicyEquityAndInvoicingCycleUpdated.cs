namespace RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Contracts.Events;

using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Models;

/// <summary>
/// Event published when a billing cycle is updated
/// </summary>
public record PolicyEquityAndInvoicingCycleUpdated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string AccountId,
    PolicyEquityAndInvoicingCycle OldPolicyEquityAndInvoicingCycle,
    PolicyEquityAndInvoicingCycle NewPolicyEquityAndInvoicingCycle,
    string ChangeReason,
    string IdempotencyKey
);
