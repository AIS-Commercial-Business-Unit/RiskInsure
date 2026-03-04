namespace RiskInsure.Policy.Domain.Repositories;

using RiskInsure.Policy.Domain.Models;

public interface IPolicyLifecycleTermStateRepository
{
    Task<PolicyLifecycleTermState?> GetByPolicyTermIdAsync(string policyTermId);

    Task<IEnumerable<PolicyLifecycleTermState>> GetByPolicyIdAsync(string policyId);

    Task<PolicyLifecycleTermState> CreateAsync(PolicyLifecycleTermState state);

    Task<PolicyLifecycleTermState> UpdateAsync(PolicyLifecycleTermState state);
}
