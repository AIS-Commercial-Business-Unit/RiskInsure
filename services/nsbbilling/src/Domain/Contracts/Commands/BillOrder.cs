namespace RiskInsure.Billing.Domain.Contracts.Commands;

/// <summary>
/// Command to bill an order (internal command)
/// </summary>
public record BillOrder(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid OrderId,
    string IdempotencyKey
);
