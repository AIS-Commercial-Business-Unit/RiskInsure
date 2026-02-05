using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.FundTransferMgt.Domain.Models;
using RiskInsure.FundTransferMgt.Domain.Repositories;

namespace RiskInsure.FundTransferMgt.Infrastructure.Repositories;

public class PaymentMethodRepository : IPaymentMethodRepository
{
    private readonly Container _container;
    private readonly ILogger<PaymentMethodRepository> _logger;

    public PaymentMethodRepository(
        Container container,
        ILogger<PaymentMethodRepository> logger)
    {
        _container = container;
        _logger = logger;
    }

    public async Task<PaymentMethod?> GetByIdAsync(string paymentMethodId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Note: Cannot use point-read without knowing customerId
            // Must query instead since partition key is /customerId not /paymentMethodId
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.paymentMethodId = @paymentMethodId")
                .WithParameter("@paymentMethodId", paymentMethodId);

            var iterator = _container.GetItemQueryIterator<PaymentMethod>(query);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var paymentMethod = response.FirstOrDefault();
                if (paymentMethod != null)
                {
                    return paymentMethod;
                }
            }

            _logger.LogDebug("Payment method {PaymentMethodId} not found", paymentMethodId);
            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error retrieving payment method {PaymentMethodId}", paymentMethodId);
            throw;
        }
    }

    public async Task<List<PaymentMethod>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.CustomerId = @customerId AND c.Type_Discriminator = 'PaymentMethod'")
            .WithParameter("@customerId", customerId);

        var results = new List<PaymentMethod>();
        var iterator = _container.GetItemQueryIterator<PaymentMethod>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        _logger.LogInformation(
            "Found {Count} payment methods for customer {CustomerId}",
            results.Count, customerId);

        return results;
    }

    public async Task<PaymentMethod> CreateAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken = default)
    {
        paymentMethod.Id = paymentMethod.PaymentMethodId;  // Set Cosmos DB id
        paymentMethod.CreatedUtc = DateTimeOffset.UtcNow;
        paymentMethod.UpdatedUtc = DateTimeOffset.UtcNow;

        var response = await _container.CreateItemAsync(
            item: paymentMethod,
            partitionKey: new PartitionKey(paymentMethod.CustomerId),  // Partition key is customerId
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Created payment method {PaymentMethodId} for customer {CustomerId}",
            paymentMethod.PaymentMethodId, paymentMethod.CustomerId);

        return response.Resource;
    }

    public async Task<PaymentMethod> UpdateAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken = default)
    {
        paymentMethod.Id = paymentMethod.PaymentMethodId;  // Ensure Cosmos DB id is set
        paymentMethod.UpdatedUtc = DateTimeOffset.UtcNow;

        var response = await _container.ReplaceItemAsync(
            item: paymentMethod,
            id: paymentMethod.PaymentMethodId,
            partitionKey: new PartitionKey(paymentMethod.CustomerId),  // Partition key is customerId
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Updated payment method {PaymentMethodId}",
            paymentMethod.PaymentMethodId);

        return response.Resource;
    }

    public async Task DeleteAsync(string paymentMethodId, CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(paymentMethodId, cancellationToken);
        if (existing == null)
        {
            _logger.LogWarning("Cannot delete - payment method {PaymentMethodId} not found", paymentMethodId);
            return;
        }

        // Soft delete - mark as Inactive
        existing.Status = PaymentMethodStatus.Inactive;
        await UpdateAsync(existing, cancellationToken);

        _logger.LogInformation(
            "Deleted (soft) payment method {PaymentMethodId}",
            paymentMethodId);
    }
}
