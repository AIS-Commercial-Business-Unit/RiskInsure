namespace RiskInsure.NsbShipping.Domain.Repositories;

using RiskInsure.NsbShipping.Domain.Models;

public interface IInventoryReservationRepository
{
    Task<InventoryReservation?> GetByOrderIdAsync(Guid orderId);
    Task CreateAsync(InventoryReservation reservation);
}
