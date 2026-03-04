namespace RiskInsure.Policy.Domain.Repositories;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.Policy.Domain.Models;
using System.Net;

public class PolicyLifecycleTermStateRepository : IPolicyLifecycleTermStateRepository
{
    private readonly Container _container;
    private readonly ILogger<PolicyLifecycleTermStateRepository> _logger;

    public PolicyLifecycleTermStateRepository(
        Container container,
        ILogger<PolicyLifecycleTermStateRepository> logger)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PolicyLifecycleTermState?> GetByPolicyTermIdAsync(string policyTermId)
    {
        try
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.policyTermId = @policyTermId AND c.documentType = @documentType")
                .WithParameter("@policyTermId", policyTermId)
                .WithParameter("@documentType", "PolicyLifecycleTermState");

            var iterator = _container.GetItemQueryIterator<PolicyLifecycleTermState>(query);
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var state = response.FirstOrDefault();
                if (state is not null)
                {
                    return state;
                }
            }

            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<PolicyLifecycleTermState>> GetByPolicyIdAsync(string policyId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.policyId = @policyId AND c.documentType = @documentType ORDER BY c.createdUtc DESC")
            .WithParameter("@policyId", policyId)
            .WithParameter("@documentType", "PolicyLifecycleTermState");

        var iterator = _container.GetItemQueryIterator<PolicyLifecycleTermState>(query);
        var results = new List<PolicyLifecycleTermState>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public async Task<PolicyLifecycleTermState> CreateAsync(PolicyLifecycleTermState state)
    {
        state.Id = state.PolicyTermId;
        state.CreatedUtc = DateTimeOffset.UtcNow;
        state.UpdatedUtc = DateTimeOffset.UtcNow;

        var response = await _container.CreateItemAsync(
            state,
            new PartitionKey(state.PolicyId));

        _logger.LogInformation(
            "Created lifecycle state for PolicyId {PolicyId} PolicyTermId {PolicyTermId}",
            state.PolicyId,
            state.PolicyTermId);

        return response.Resource;
    }

    public async Task<PolicyLifecycleTermState> UpdateAsync(PolicyLifecycleTermState state)
    {
        state.UpdatedUtc = DateTimeOffset.UtcNow;

        var requestOptions = new ItemRequestOptions
        {
            IfMatchEtag = state.ETag
        };

        var response = await _container.ReplaceItemAsync(
            state,
            state.Id,
            new PartitionKey(state.PolicyId),
            requestOptions);

        return response.Resource;
    }
}
