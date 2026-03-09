namespace RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Contracts.Events;

using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Models;

/// <summary>
/// Event published when a new billing account is created
/// </summary>
public record PolicyEquityAndInvoicingAccountCreated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string AccountId,
    string CustomerId,
    string PolicyNumber,
    string PolicyHolderName,
    decimal CurrentPremiumOwed,
    PolicyEquityAndInvoicingCycle PolicyEquityAndInvoicingCycle,
    DateTimeOffset EffectiveDate,
    string IdempotencyKey
);
