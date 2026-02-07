namespace RiskInsure.NsbSales.Domain.Repositories;

using RiskInsure.NsbSales.Domain.Models;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId);
    Task<Order> CreateAsync(Order order);
    Task<Order> UpdateAsync(Order order);
}
