namespace RiskInsure.Policy.Domain.Repositories;

public interface IPolicyRepository
{
    Task<Models.Policy?> GetByIdAsync(string policyId);
    Task<Models.Policy?> GetByQuoteIdAsync(string quoteId);
    Task<Models.Policy?> GetByPolicyNumberAsync(string policyNumber);
    Task<IEnumerable<Models.Policy>> GetByCustomerIdAsync(string customerId);
    Task<Models.Policy> CreateAsync(Models.Policy policy);
    Task<Models.Policy> UpdateAsync(Models.Policy policy);
    Task<IEnumerable<Models.Policy>> GetExpirablePoliciesAsync(DateTimeOffset currentDate);
}
