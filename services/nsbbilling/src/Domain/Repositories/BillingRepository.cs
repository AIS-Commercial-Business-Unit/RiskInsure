namespace RiskInsure.Billing.Domain.Repositories;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.Billing.Domain.Models;
using System.Net;

public class BillingRepository : IBillingRepository
{
    private readonly Container _container;
    private readonly ILogger<BillingRepository> _logger;

    public BillingRepository(Container container, ILogger<BillingRepository> logger)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BillingDocument?> GetByOrderIdAsync(Guid orderId)
    {
        try
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.orderId = @orderId AND c.type = 'Billing'")
                .WithParameter("@orderId", orderId);

            var iterator = _container.GetItemQueryIterator<BillingDocument>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(orderId.ToString())
                });

            var results = await iterator.ReadNextAsync();
            return results.FirstOrDefault();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<BillingDocument> CreateAsync(BillingDocument billing)
    {
        var response = await _container.CreateItemAsync(
            billing,
            new PartitionKey(billing.OrderId.ToString()));
        
        _logger.LogInformation(
            "Billing record {BillingId} created for OrderId {OrderId}",
            billing.Id, billing.OrderId);
        
        return response.Resource;
    }

    public async Task<BillingDocument> UpdateAsync(BillingDocument billing)
    {
        var response = await _container.ReplaceItemAsync(
            billing,
            billing.Id,
            new PartitionKey(billing.OrderId.ToString()),
            new ItemRequestOptions { IfMatchEtag = billing.ETag });
        
        _logger.LogInformation(
            "Billing record {BillingId} updated for OrderId {OrderId}",
            billing.Id, billing.OrderId);
        
        return response.Resource;
    }
}
