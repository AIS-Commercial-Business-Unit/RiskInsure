using RiskInsure.FundTransferMgt.Domain.Models;

namespace RiskInsure.FundTransferMgt.Domain.Repositories;

/// <summary>
/// Repository for payment methods
/// Container: PaymentMethods, Partition Key: /paymentMethodId
/// </summary>
public interface IPaymentMethodRepository
{
    /// <summary>
    /// Get payment method by ID
    /// </summary>
    Task<PaymentMethod?> GetByIdAsync(string paymentMethodId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all payment methods for a customer
    /// Cross-partition query using customerId
    /// </summary>
    Task<List<PaymentMethod>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create new payment method
    /// </summary>
    Task<PaymentMethod> CreateAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update payment method
    /// </summary>
    Task<PaymentMethod> UpdateAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete payment method (soft delete - mark as Inactive)
    /// </summary>
    Task DeleteAsync(string paymentMethodId, CancellationToken cancellationToken = default);
}
