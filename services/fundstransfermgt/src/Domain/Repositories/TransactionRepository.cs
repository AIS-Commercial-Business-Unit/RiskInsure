using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.FundTransferMgt.Domain.Models;

namespace RiskInsure.FundTransferMgt.Domain.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly Container _container;
    private readonly ILogger<TransactionRepository> _logger;

    public TransactionRepository(
        Container container,
        ILogger<TransactionRepository> logger)
    {
        _container = container;
        _logger = logger;
    }

    public async Task<FundTransfer?> GetTransferByIdAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Note: Cannot use point-read without knowing customerId
            // Must query instead since partition key is /customerId not /transactionId
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.transactionId = @transactionId")
                .WithParameter("@transactionId", transactionId);

            var iterator = _container.GetItemQueryIterator<FundTransfer>(query);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var transfer = response.FirstOrDefault();
                if (transfer != null)
                {
                    return transfer;
                }
            }

            _logger.LogDebug("Transfer {TransactionId} not found", transactionId);
            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error retrieving transfer {TransactionId}", transactionId);
            throw;
        }
    }

    public async Task<List<FundTransfer>> GetTransfersByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.customerId = @customerId AND c.type_Discriminator = 'FundTransfer'")
            .WithParameter("@customerId", customerId);

        var results = new List<FundTransfer>();
        var iterator = _container.GetItemQueryIterator<FundTransfer>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        _logger.LogInformation(
            "Found {Count} transfers for customer {CustomerId}",
            results.Count, customerId);

        return results;
    }

    public async Task<FundTransfer> CreateTransferAsync(FundTransfer transfer, CancellationToken cancellationToken = default)
    {
        transfer.Id = transfer.TransactionId;  // Set Cosmos DB id
        transfer.InitiatedUtc = DateTimeOffset.UtcNow;

        var response = await _container.CreateItemAsync(
            item: transfer,
            partitionKey: new PartitionKey(transfer.CustomerId),  // Partition key is customerId
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Created transfer {TransactionId} for customer {CustomerId}, amount {Amount}",
            transfer.TransactionId, transfer.CustomerId, transfer.Amount);

        return response.Resource;
    }

    public async Task<FundTransfer> UpdateTransferAsync(FundTransfer transfer, CancellationToken cancellationToken = default)
    {
        transfer.Id = transfer.TransactionId;  // Ensure Cosmos DB id is set
        
        var response = await _container.ReplaceItemAsync(
            item: transfer,
            id: transfer.TransactionId,
            partitionKey: new PartitionKey(transfer.CustomerId),  // Partition key is customerId
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Updated transfer {TransactionId} to status {Status}",
            transfer.TransactionId, transfer.Status);

        return response.Resource;
    }

    public async Task<Refund?> GetRefundByIdAsync(string refundId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Note: Cannot use point-read without knowing customerId
            // Must query instead since partition key is /customerId not /refundId
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.refundId = @refundId")
                .WithParameter("@refundId", refundId);

            var iterator = _container.GetItemQueryIterator<Refund>(query);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var refund = response.FirstOrDefault();
                if (refund != null)
                {
                    return refund;
                }
            }

            _logger.LogDebug("Refund {RefundId} not found", refundId);
            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error retrieving refund {RefundId}", refundId);
            throw;
        }
    }

    public async Task<Refund> CreateRefundAsync(Refund refund, CancellationToken cancellationToken = default)
    {
        refund.Id = refund.RefundId;  // Set Cosmos DB id
        refund.InitiatedUtc = DateTimeOffset.UtcNow;

        var response = await _container.CreateItemAsync(
            item: refund,
            partitionKey: new PartitionKey(refund.CustomerId),  // Partition key is customerId
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Created refund {RefundId} for original transaction {OriginalTransactionId}, amount {Amount}",
            refund.RefundId, refund.OriginalTransactionId, refund.Amount);

        return response.Resource;
    }

    public async Task<Refund> UpdateRefundAsync(Refund refund, CancellationToken cancellationToken = default)
    {
        refund.Id = refund.RefundId;  // Ensure Cosmos DB id is set
        
        var response = await _container.ReplaceItemAsync(
            item: refund,
            id: refund.RefundId,
            partitionKey: new PartitionKey(refund.CustomerId),  // Partition key is customerId
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Updated refund {RefundId} to status {Status}",
            refund.RefundId, refund.Status);

        return response.Resource;
    }
}
