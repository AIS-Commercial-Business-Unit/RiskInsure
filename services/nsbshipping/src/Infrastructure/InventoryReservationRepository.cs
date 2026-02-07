namespace RiskInsure.NsbShipping.Infrastructure;

using Microsoft.Azure.Cosmos;
using RiskInsure.NsbShipping.Domain.Models;
using RiskInsure.NsbShipping.Domain.Repositories;

public class InventoryReservationRepository : IInventoryReservationRepository
{
    private readonly Container _container;
    public InventoryReservationRepository(Container container)
    {
        _container = container;
    }

    public async Task<InventoryReservation?> GetByOrderIdAsync(Guid orderId)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.orderId = @orderId AND c.documentType = 'InventoryReservation'")
            .WithParameter("@orderId", orderId);
        var iterator = _container.GetItemQueryIterator<InventoryReservation>(query);
        var results = await iterator.ReadNextAsync();
        return results.FirstOrDefault();
    }

    public async Task CreateAsync(InventoryReservation reservation)
    {
        await _container.CreateItemAsync(reservation, new PartitionKey(reservation.OrderId.ToString()));
    }
}
