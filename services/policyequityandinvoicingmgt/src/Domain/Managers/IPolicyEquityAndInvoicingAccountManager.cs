namespace RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Managers;

using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Managers.DTOs;

/// <summary>
/// Manager interface for billing account lifecycle operations
/// </summary>
public interface IPolicyEquityAndInvoicingAccountManager
{
    /// <summary>
    /// Creates a new billing account for an insurance policy
    /// </summary>
    /// <param name="dto">Account creation details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the account creation operation</returns>
    Task<PolicyEquityAndInvoicingAccountResult> CreatePolicyEquityAndInvoicingAccountAsync(
        CreatePolicyEquityAndInvoicingAccountDto dto, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates the premium owed on an existing account
    /// </summary>
    /// <param name="dto">Premium update details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the premium update operation</returns>
    Task<PolicyEquityAndInvoicingAccountResult> UpdatePremiumOwedAsync(
        UpdatePremiumOwedDto dto, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Activates a pending billing account
    /// </summary>
    /// <param name="accountId">Account identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the activation operation</returns>
    Task<PolicyEquityAndInvoicingAccountResult> ActivateAccountAsync(
        string accountId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Suspends an active billing account
    /// </summary>
    /// <param name="accountId">Account identifier</param>
    /// <param name="suspensionReason">Reason for suspension</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the suspension operation</returns>
    Task<PolicyEquityAndInvoicingAccountResult> SuspendAccountAsync(
        string accountId, 
        string suspensionReason, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Closes a billing account
    /// </summary>
    /// <param name="accountId">Account identifier</param>
    /// <param name="closureReason">Reason for closure</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the closure operation</returns>
    Task<PolicyEquityAndInvoicingAccountResult> CloseAccountAsync(
        string accountId, 
        string closureReason, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates the billing cycle for an account
    /// </summary>
    /// <param name="dto">Billing cycle update details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the billing cycle update operation</returns>
    Task<PolicyEquityAndInvoicingAccountResult> UpdatePolicyEquityAndInvoicingCycleAsync(
        UpdatePolicyEquityAndInvoicingCycleDto dto, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves all billing accounts
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all billing accounts</returns>
    Task<IEnumerable<Models.PolicyEquityAndInvoicingAccount>> GetAllAccountsAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves a single billing account by ID
    /// </summary>
    /// <param name="accountId">Account identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Billing account if found, null otherwise</returns>
    Task<Models.PolicyEquityAndInvoicingAccount?> GetAccountByIdAsync(
        string accountId, 
        CancellationToken cancellationToken = default);
}
