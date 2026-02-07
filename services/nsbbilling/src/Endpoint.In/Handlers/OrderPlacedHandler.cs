namespace RiskInsure.Billing.Endpoint.In.Handlers;

using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.Billing.Domain.Contracts.Commands;
using RiskInsure.Billing.Domain.Managers;
using RiskInsure.PublicContracts.Events;

/// <summary>
/// Handles OrderPlaced events from Sales domain
/// Policy: MustBillOnOrderPlaced
/// </summary>
public class OrderPlacedHandler : IHandleMessages<OrderPlaced>
{
    private readonly IBillingManager _manager;
    private readonly ILogger<OrderPlacedHandler> _logger;

    public OrderPlacedHandler(
        IBillingManager manager,
        ILogger<OrderPlacedHandler> logger)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(OrderPlaced message, IMessageHandlerContext context)
    {
        _logger.LogInformation(
            "Received OrderPlaced, OrderId = {OrderId} - Charging credit card...",
            message.OrderId);

        // Call domain manager to bill order
        var billing = await _manager.BillOrderAsync(
            new BillOrder(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                OrderId: message.OrderId,
                IdempotencyKey: $"BillOrder-{message.OrderId}"
            ));

        _logger.LogInformation(
            "Publishing OrderBilled for OrderId = {OrderId}",
            message.OrderId);

        // Publish resulting event
        await context.Publish(new OrderBilled(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            OrderId: billing.OrderId,
            IdempotencyKey: $"OrderBilled-{message.OrderId}"
        ));

        _logger.LogInformation(
            "OrderBilled published for OrderId = {OrderId}",
            message.OrderId);
    }
}
