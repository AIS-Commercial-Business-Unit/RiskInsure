namespace RiskInsure.NsbSales.Domain.Managers;

using RiskInsure.NsbSales.Domain.Contracts.Commands;
using RiskInsure.NsbSales.Domain.Models;

public interface IOrderManager
{
    Task<Order> PlaceOrderAsync(PlaceOrder command);
    Task<Order> GetOrderAsync(Guid orderId);
}
