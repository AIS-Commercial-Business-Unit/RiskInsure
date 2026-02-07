namespace RiskInsure.NsbSales.Domain.Contracts.Commands;

/// <summary>
/// Command to place a new sales order
/// </summary>
public record PlaceOrder(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid OrderId,
    string IdempotencyKey
);
