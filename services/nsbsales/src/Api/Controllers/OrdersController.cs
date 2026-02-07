namespace RiskInsure.NsbSales.Api.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NServiceBus;
using RiskInsure.NsbSales.Api.Models;
using RiskInsure.NsbSales.Domain.Contracts.Commands;
using RiskInsure.NsbSales.Domain.Managers;
using RiskInsure.PublicContracts.Events;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderManager _orderManager;
    private readonly IMessageSession _messageSession;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IOrderManager orderManager,
        IMessageSession messageSession,
        ILogger<OrdersController> logger)
    {
        _orderManager = orderManager ?? throw new ArgumentNullException(nameof(orderManager));
        _messageSession = messageSession ?? throw new ArgumentNullException(nameof(messageSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Place a new order
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        try
        {
            _logger.LogInformation("Received request to place order {OrderId}", request.OrderId);

            var command = new PlaceOrder(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                OrderId: request.OrderId,
                IdempotencyKey: $"PlaceOrder-{request.OrderId}"
            );

            // Process order through manager
            var order = await _orderManager.PlaceOrderAsync(command);

            // Publish OrderPlaced event
            var orderPlacedEvent = new OrderPlaced(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                OrderId: order.OrderId,
                IdempotencyKey: $"OrderPlaced-{order.OrderId}"
            );

            await _messageSession.Publish(orderPlacedEvent);

            _logger.LogInformation("Order {OrderId} placed and event published", order.OrderId);

            return CreatedAtAction(
                nameof(GetOrder),
                new { orderId = order.OrderId },
                new { orderId = order.OrderId, status = order.Status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing order {OrderId}", request.OrderId);
            return StatusCode(500, new { error = "Failed to place order" });
        }
    }

    /// <summary>
    /// Get order by ID
    /// </summary>
    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(Guid orderId)
    {
        try
        {
            var order = await _orderManager.GetOrderAsync(orderId);
            return Ok(order);
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { error = $"Order {orderId} not found" });
        }
    }
}
