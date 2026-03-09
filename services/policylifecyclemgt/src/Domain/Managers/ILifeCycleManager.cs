namespace RiskInsure.PolicyLifeCycleMgt.Domain.Managers;

using RiskInsure.PublicContracts.Events;

public interface ILifeCycleManager
{
    Task<Models.LifeCycle> CreateFromQuoteAsync(QuoteAccepted quote);
    Task<Models.LifeCycle> IssueLifeCycleAsync(string policyId);
    Task<Models.LifeCycle> CancelLifeCycleAsync(string policyId, DateTimeOffset cancellationDate, string reason);
    Task<Models.LifeCycle> ReinstateLifeCycleAsync(string policyId);
    Task<Models.LifeCycle> GetLifeCycleAsync(string policyId);
    Task<IEnumerable<Models.LifeCycle>> GetCustomerLifeCyclesAsync(string customerId);
}
