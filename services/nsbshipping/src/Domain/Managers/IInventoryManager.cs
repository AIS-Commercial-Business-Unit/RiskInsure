namespace RiskInsure.NsbShipping.Domain.Managers;

using RiskInsure.NsbShipping.Domain.Models;

public interface IInventoryManager
{
    Task<InventoryReservation> ReserveInventoryAsync(Guid orderId);
}
