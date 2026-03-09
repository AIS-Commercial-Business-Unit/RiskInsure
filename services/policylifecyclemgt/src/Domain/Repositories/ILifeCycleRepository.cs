namespace RiskInsure.PolicyLifeCycleMgt.Domain.Repositories;

public interface ILifeCycleRepository
{
    Task<Models.LifeCycle?> GetByIdAsync(string policyId);
    Task<Models.LifeCycle?> GetByQuoteIdAsync(string quoteId);
    Task<Models.LifeCycle?> GetByPolicyNumberAsync(string policyNumber);
    Task<IEnumerable<Models.LifeCycle>> GetByCustomerIdAsync(string customerId);
    Task<Models.LifeCycle> CreateAsync(Models.LifeCycle policy);
    Task<Models.LifeCycle> UpdateAsync(Models.LifeCycle policy);
    Task<IEnumerable<Models.LifeCycle>> GetExpirablePoliciesAsync(DateTimeOffset currentDate);
}
