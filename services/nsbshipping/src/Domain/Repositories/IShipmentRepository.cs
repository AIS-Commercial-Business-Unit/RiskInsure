namespace RiskInsure.NsbShipping.Domain.Repositories;

using RiskInsure.NsbShipping.Domain.Models;

public interface IShipmentRepository
{
    Task<Shipment?> GetByOrderIdAsync(Guid orderId);
    Task CreateAsync(Shipment shipment);
}
