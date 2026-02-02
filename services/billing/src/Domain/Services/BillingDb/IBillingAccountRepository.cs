namespace RiskInsure.Billing.Domain.Services.BillingDb;

using RiskInsure.Billing.Domain.Models;

/// <summary>
/// Repository interface for billing account data access
/// </summary>
public interface IBillingAccountRepository
{
    /// <summary>
    /// Retrieves a billing account by its unique identifier
    /// </summary>
    /// <param name="accountId">The account identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The billing account if found, null otherwise</returns>
    Task<BillingAccount?> GetByAccountIdAsync(string accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves a billing account by customer ID
    /// </summary>
    /// <param name="customerId">The customer identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The billing account if found, null otherwise</returns>
    Task<BillingAccount?> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new billing account
    /// </summary>
    /// <param name="account">The billing account to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CreateAsync(BillingAccount account, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing billing account with optimistic concurrency control
    /// </summary>
    /// <param name="account">The billing account to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="InvalidOperationException">Thrown when ETag mismatch (concurrency conflict)</exception>
    Task UpdateAsync(BillingAccount account, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Records a payment to a billing account atomically
    /// </summary>
    /// <param name="accountId">The account identifier</param>
    /// <param name="amount">The payment amount to apply</param>
    /// <param name="referenceNumber">Reference number from payment source</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated billing account</returns>
    Task<BillingAccount> RecordPaymentAsync(
        string accountId, 
        decimal amount, 
        string referenceNumber, 
        CancellationToken cancellationToken = default);
}
