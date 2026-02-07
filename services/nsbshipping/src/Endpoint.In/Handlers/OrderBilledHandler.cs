namespace RiskInsure.NsbShipping.Endpoint.In.Handlers;

using NServiceBus;
using RiskInsure.PublicContracts.Events;
using RiskInsure.NsbShipping.Domain.Managers;
using Microsoft.Extensions.Logging;

public class OrderBilledHandler : IHandleMessages<OrderBilled>
{
    private readonly IShippingManager _shippingManager;
    private readonly ILogger<OrderBilledHandler> _logger;

    public OrderBilledHandler(IShippingManager shippingManager, ILogger<OrderBilledHandler> logger)
    {
        _shippingManager = shippingManager;
        _logger = logger;
    }

    public async Task Handle(OrderBilled message, IMessageHandlerContext context)
    {
        _logger.LogInformation("Received OrderBilled, OrderId = {OrderId} - Verify Package and Ship...", message.OrderId);
        await _shippingManager.ShipOrderAsync(message.OrderId);
        // Publish OrderShipped event here if needed
    }
}
