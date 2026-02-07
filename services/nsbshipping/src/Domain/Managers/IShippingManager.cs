namespace RiskInsure.NsbShipping.Domain.Managers;

using RiskInsure.NsbShipping.Domain.Models;

public interface IShippingManager
{
    Task<Shipment> ShipOrderAsync(Guid orderId);
}
