namespace RiskInsure.Policy.Domain.Managers;

using RiskInsure.PublicContracts.Events;

public interface IPolicyManager
{
    Task<Models.Policy> CreateFromQuoteAsync(QuoteAccepted quote);
    Task<Models.Policy> IssuePolicyAsync(string policyId);
    Task<Models.Policy> CancelPolicyAsync(string policyId, DateTimeOffset cancellationDate, string reason);
    Task<Models.Policy> ReinstatePolicyAsync(string policyId);
    Task<Models.Policy> GetPolicyAsync(string policyId);
    Task<IEnumerable<Models.Policy>> GetCustomerPoliciesAsync(string customerId);
}
