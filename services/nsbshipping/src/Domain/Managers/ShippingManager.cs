namespace RiskInsure.NsbShipping.Domain.Managers;

using RiskInsure.NsbShipping.Domain.Models;
using RiskInsure.NsbShipping.Domain.Repositories;
using Microsoft.Extensions.Logging;

public class ShippingManager : IShippingManager
{
    private readonly IShipmentRepository _repository;
    private readonly ILogger<ShippingManager> _logger;

    public ShippingManager(IShipmentRepository repository, ILogger<ShippingManager> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Shipment> ShipOrderAsync(Guid orderId)
    {
        var existing = await _repository.GetByOrderIdAsync(orderId);
        if (existing != null)
        {
            _logger.LogInformation("Order already shipped for OrderId {OrderId}, skipping", orderId);
            return existing;
        }
        var shipment = new Shipment
        {
            Id = Guid.NewGuid().ToString(),
            OrderId = orderId,
            TrackingNumber = "TRK-" + Guid.NewGuid().ToString()[..8],
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };
        await _repository.CreateAsync(shipment);
        _logger.LogInformation("Shipped order for OrderId {OrderId}", orderId);
        return shipment;
    }
}
