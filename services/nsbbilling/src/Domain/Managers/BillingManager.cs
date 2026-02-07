namespace RiskInsure.Billing.Domain.Managers;

using Microsoft.Extensions.Logging;
using RiskInsure.Billing.Domain.Contracts.Commands;
using RiskInsure.Billing.Domain.Models;
using RiskInsure.Billing.Domain.Repositories;

public class BillingManager : IBillingManager
{
    private readonly IBillingRepository _repository;
    private readonly ILogger<BillingManager> _logger;

    public BillingManager(
        IBillingRepository repository,
        ILogger<BillingManager> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BillingDocument> BillOrderAsync(BillOrder command)
    {
        _logger.LogInformation(
            "Billing order {OrderId}",
            command.OrderId);

        // Check for duplicate billing (idempotency)
        var existing = await _repository.GetByOrderIdAsync(command.OrderId);
        if (existing != null)
        {
            _logger.LogInformation(
                "Order {OrderId} already billed, returning existing",
                command.OrderId);
            return existing;
        }

        // Simulate payment processing (charge credit card)
        _logger.LogInformation(
            "Processing payment for OrderId {OrderId} - Charging credit card...",
            command.OrderId);

        // Create billing record
        var billing = new BillingDocument
        {
            Id = Guid.NewGuid().ToString(),
            OrderId = command.OrderId,
            Status = "Charged",
            ChargedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var created = await _repository.CreateAsync(billing);

        _logger.LogInformation(
            "Order {OrderId} billed successfully",
            command.OrderId);

        return created;
    }
}
