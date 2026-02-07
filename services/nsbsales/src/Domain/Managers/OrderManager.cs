namespace RiskInsure.NsbSales.Domain.Managers;

using Microsoft.Extensions.Logging;
using RiskInsure.NsbSales.Domain.Contracts.Commands;
using RiskInsure.NsbSales.Domain.Models;
using RiskInsure.NsbSales.Domain.Repositories;

public class OrderManager : IOrderManager
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<OrderManager> _logger;

    public OrderManager(
        IOrderRepository repository,
        ILogger<OrderManager> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Order> PlaceOrderAsync(PlaceOrder command)
    {
        _logger.LogInformation(
            "Placing order {OrderId}",
            command.OrderId);

        // Check for duplicate order (idempotency)
        var existing = await _repository.GetByIdAsync(command.OrderId);
        if (existing != null)
        {
            _logger.LogInformation("Order {OrderId} already exists, returning existing", command.OrderId);
            return existing;
        }

        // Create order
        var order = new Order
        {
            Id = command.OrderId.ToString(),
            OrderId = command.OrderId,
            Status = "Placed",
            PlacedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var created = await _repository.CreateAsync(order);

        _logger.LogInformation("Order {OrderId} placed successfully", command.OrderId);

        return created;
    }

    public async Task<Order> GetOrderAsync(Guid orderId)
    {
        var order = await _repository.GetByIdAsync(orderId);
        if (order == null)
        {
            throw new InvalidOperationException($"Order {orderId} not found");
        }
        return order;
    }
}
