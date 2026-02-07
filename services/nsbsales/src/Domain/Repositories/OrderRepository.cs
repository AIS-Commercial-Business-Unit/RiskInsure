namespace RiskInsure.NsbSales.Domain.Repositories;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.NsbSales.Domain.Models;
using System.Net;

public class OrderRepository : IOrderRepository
{
    private readonly Container _container;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(Container container, ILogger<OrderRepository> logger)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Order?> GetByIdAsync(Guid orderId)
    {
        try
        {
            var response = await _container.ReadItemAsync<Order>(
                orderId.ToString(),
                new PartitionKey(orderId.ToString()));
            
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Order> CreateAsync(Order order)
    {
        var response = await _container.CreateItemAsync(
            order,
            new PartitionKey(order.OrderId.ToString()));
        
        _logger.LogInformation("Order {OrderId} created", order.OrderId);
        return response.Resource;
    }

    public async Task<Order> UpdateAsync(Order order)
    {
        var response = await _container.ReplaceItemAsync(
            order,
            order.Id,
            new PartitionKey(order.OrderId.ToString()),
            new ItemRequestOptions { IfMatchEtag = order.ETag });
        
        _logger.LogInformation("Order {OrderId} updated", order.OrderId);
        return response.Resource;
    }
}
