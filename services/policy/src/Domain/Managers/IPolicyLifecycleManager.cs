namespace RiskInsure.Policy.Domain.Managers;

using RiskInsure.Policy.Domain.Contracts.Commands;
using RiskInsure.Policy.Domain.Contracts.Events;
using RiskInsure.Policy.Domain.Models;

public interface IPolicyLifecycleManager
{
    Task<PolicyLifecycleTermState?> GetLifecycleStateAsync(string policyTermId);

    Task<IEnumerable<PolicyLifecycleTermState>> GetLifecycleStatesByPolicyIdAsync(string policyId);

    Task<PolicyLifecycleTermState> StartLifecycleAsync(StartPolicyLifecycle command);

    Task<PolicyLifecycleTermState?> ApplyMilestoneAsync(ApplyPolicyLifecycleMilestone command);

    Task<PolicyLifecycleTermState?> ProcessEquityUpdateAsync(ProcessPolicyEquityUpdate command);

    Task<PolicyLifecycleTermState?> HandleRenewalAcceptedAsync(PolicyRenewalAccepted policyRenewalAccepted);
}
