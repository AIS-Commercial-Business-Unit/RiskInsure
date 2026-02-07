namespace RiskInsure.NsbShipping.Endpoint.In.Handlers;

using NServiceBus;
using RiskInsure.PublicContracts.Events;
using RiskInsure.NsbShipping.Domain.Managers;
using Microsoft.Extensions.Logging;

public class OrderPlacedHandler : IHandleMessages<OrderPlaced>
{
    private readonly IInventoryManager _inventoryManager;
    private readonly ILogger<OrderPlacedHandler> _logger;

    public OrderPlacedHandler(IInventoryManager inventoryManager, ILogger<OrderPlacedHandler> logger)
    {
        _inventoryManager = inventoryManager;
        _logger = logger;
    }

    public async Task Handle(OrderPlaced message, IMessageHandlerContext context)
    {
        _logger.LogInformation("Received OrderPlaced, OrderId = {OrderId} - Order Packaging Start...", message.OrderId);
        await _inventoryManager.ReserveInventoryAsync(message.OrderId);
        // Publish InventoryReserved event here if needed
    }
}
