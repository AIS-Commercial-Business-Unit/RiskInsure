namespace RiskInsure.NsbShipping.Infrastructure;

using Microsoft.Azure.Cosmos;
using RiskInsure.NsbShipping.Domain.Models;
using RiskInsure.NsbShipping.Domain.Repositories;

public class ShipmentRepository : IShipmentRepository
{
    private readonly Container _container;
    public ShipmentRepository(Container container)
    {
        _container = container;
    }

    public async Task<Shipment?> GetByOrderIdAsync(Guid orderId)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.orderId = @orderId AND c.documentType = 'Shipment'")
            .WithParameter("@orderId", orderId);
        var iterator = _container.GetItemQueryIterator<Shipment>(query);
        var results = await iterator.ReadNextAsync();
        return results.FirstOrDefault();
    }

    public async Task CreateAsync(Shipment shipment)
    {
        await _container.CreateItemAsync(shipment, new PartitionKey(shipment.OrderId.ToString()));
    }
}
