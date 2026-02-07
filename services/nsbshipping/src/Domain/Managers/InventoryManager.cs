namespace RiskInsure.NsbShipping.Domain.Managers;

using RiskInsure.NsbShipping.Domain.Models;
using RiskInsure.NsbShipping.Domain.Repositories;
using Microsoft.Extensions.Logging;

public class InventoryManager : IInventoryManager
{
    private readonly IInventoryReservationRepository _repository;
    private readonly ILogger<InventoryManager> _logger;

    public InventoryManager(IInventoryReservationRepository repository, ILogger<InventoryManager> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<InventoryReservation> ReserveInventoryAsync(Guid orderId)
    {
        var existing = await _repository.GetByOrderIdAsync(orderId);
        if (existing != null)
        {
            _logger.LogInformation("Inventory already reserved for OrderId {OrderId}, skipping", orderId);
            return existing;
        }
        var reservation = new InventoryReservation
        {
            Id = Guid.NewGuid().ToString(),
            OrderId = orderId,
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };
        await _repository.CreateAsync(reservation);
        _logger.LogInformation("Reserved inventory for OrderId {OrderId}", orderId);
        return reservation;
    }
}
