using RiskInsure.FundTransferMgt.Domain.Models;

namespace RiskInsure.FundTransferMgt.Domain.Managers;

public interface IFundTransferManager
{
    Task<FundTransfer> InitiateTransferAsync(
        string customerId,
        string paymentMethodId,
        decimal amount,
        string purpose,
        CancellationToken cancellationToken = default);
    
    Task<Refund> ProcessRefundAsync(
        string originalTransactionId,
        decimal amount,
        string reason,
        CancellationToken cancellationToken = default);
    
    Task<FundTransfer?> GetTransferAsync(
        string transactionId,
        CancellationToken cancellationToken = default);
    
    Task<List<FundTransfer>> GetCustomerTransfersAsync(
        string customerId,
        CancellationToken cancellationToken = default);
}
