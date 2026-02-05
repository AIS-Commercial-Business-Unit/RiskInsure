using RiskInsure.FundTransferMgt.Domain.Models;

namespace RiskInsure.FundTransferMgt.Domain.Repositories;

/// <summary>
/// Repository for fund transfers and refunds
/// Container: Transactions, Partition Key: /transactionId
/// </summary>
public interface ITransactionRepository
{
    /// <summary>
    /// Get fund transfer by transaction ID
    /// </summary>
    Task<FundTransfer?> GetTransferByIdAsync(string transactionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all transfers for a customer
    /// Cross-partition query using customerId
    /// </summary>
    Task<List<FundTransfer>> GetTransfersByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create new fund transfer
    /// </summary>
    Task<FundTransfer> CreateTransferAsync(FundTransfer transfer, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update fund transfer status
    /// </summary>
    Task<FundTransfer> UpdateTransferAsync(FundTransfer transfer, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get refund by refund ID
    /// </summary>
    Task<Refund?> GetRefundByIdAsync(string refundId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create new refund
    /// </summary>
    Task<Refund> CreateRefundAsync(Refund refund, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update refund status
    /// </summary>
    Task<Refund> UpdateRefundAsync(Refund refund, CancellationToken cancellationToken = default);
}
