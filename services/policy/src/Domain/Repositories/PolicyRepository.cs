namespace RiskInsure.Policy.Domain.Repositories;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Net;

public class PolicyRepository : IPolicyRepository
{
    private readonly Container _container;
    private readonly ILogger<PolicyRepository> _logger;

    public PolicyRepository(Container container, ILogger<PolicyRepository> logger)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Models.Policy?> GetByIdAsync(string policyId)
    {
        try
        {
            var response = await _container.ReadItemAsync<Models.Policy>(
                policyId,
                new PartitionKey(policyId));

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Models.Policy?> GetByQuoteIdAsync(string quoteId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.quoteId = @quoteId")
            .WithParameter("@quoteId", quoteId);

        var iterator = _container.GetItemQueryIterator<Models.Policy>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            var policy = response.FirstOrDefault();
            if (policy != null)
                return policy;
        }

        return null;
    }

    public async Task<Models.Policy?> GetByPolicyNumberAsync(string policyNumber)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.policyNumber = @policyNumber")
            .WithParameter("@policyNumber", policyNumber);

        var iterator = _container.GetItemQueryIterator<Models.Policy>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            var policy = response.FirstOrDefault();
            if (policy != null)
                return policy;
        }

        return null;
    }

    public async Task<IEnumerable<Models.Policy>> GetByCustomerIdAsync(string customerId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.customerId = @customerId ORDER BY c.createdUtc DESC")
            .WithParameter("@customerId", customerId);

        var iterator = _container.GetItemQueryIterator<Models.Policy>(query);
        var policies = new List<Models.Policy>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            policies.AddRange(response);
        }

        return policies;
    }

    public async Task<Models.Policy> CreateAsync(Models.Policy policy)
    {
        policy.Id = policy.PolicyId;  // Ensure Cosmos DB id matches partition key
        policy.CreatedUtc = DateTimeOffset.UtcNow;
        policy.UpdatedUtc = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Creating policy document: Id={Id}, PolicyId={PolicyId}, PolicyNumber={PolicyNumber}",
            policy.Id, policy.PolicyId, policy.PolicyNumber);

        var response = await _container.CreateItemAsync(
            policy,
            new PartitionKey(policy.PolicyId));

        _logger.LogInformation(
            "Created policy {PolicyNumber} for customer {CustomerId}",
            policy.PolicyNumber, policy.CustomerId);

        return response.Resource;
    }

    public async Task<Models.Policy> UpdateAsync(Models.Policy policy)
    {
        policy.UpdatedUtc = DateTimeOffset.UtcNow;

        var requestOptions = new ItemRequestOptions
        {
            IfMatchEtag = policy.ETag
        };

        try
        {
            var response = await _container.ReplaceItemAsync(
                policy,
                policy.PolicyId,
                new PartitionKey(policy.PolicyId),
                requestOptions);

            _logger.LogInformation(
                "Updated policy {PolicyNumber} to status {Status}",
                policy.PolicyNumber, policy.Status);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            _logger.LogWarning(
                "Optimistic concurrency conflict updating policy {PolicyId}",
                policy.PolicyId);
            throw new InvalidOperationException("Policy was modified by another process. Please retry.", ex);
        }
    }

    public async Task<IEnumerable<Models.Policy>> GetExpirablePoliciesAsync(DateTimeOffset currentDate)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.expirationDate < @currentDate AND c.status = @status")
            .WithParameter("@currentDate", currentDate)
            .WithParameter("@status", "Active");

        var iterator = _container.GetItemQueryIterator<Models.Policy>(query);
        var policies = new List<Models.Policy>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            policies.AddRange(response);
        }

        return policies;
    }
}
