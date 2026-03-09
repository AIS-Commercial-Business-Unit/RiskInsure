namespace RiskInsure.CustomerRelationshipsMgt.Domain.Repositories;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.CustomerRelationshipsMgt.Domain.Models;

public class RelationshipRepository : IRelationshipRepository
{
    private readonly Container _container;
    private readonly ILogger<RelationshipRepository> _logger;

    public RelationshipRepository(Container container, ILogger<RelationshipRepository> logger)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Relationship?> GetByIdAsync(string relationshipId)
    {
        try
        {
            var response = await _container.ReadItemAsync<Relationship>(
                relationshipId,
                new PartitionKey(relationshipId));

            _logger.LogInformation(
                "Retrieved relationship {RelationshipId}",
                relationshipId);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "Relationship {RelationshipId} not found",
                relationshipId);
            return null;
        }
    }

    public async Task<Relationship?> GetByEmailAsync(string email)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.documentType = @documentType AND c.email = @email")
            .WithParameter("@documentType", "Relationship")
            .WithParameter("@email", email);

        var iterator = _container.GetItemQueryIterator<Relationship>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            var relationship = response.FirstOrDefault();
            
            if (relationship != null)
            {
                _logger.LogInformation(
                    "Found relationship by email {Email}: {RelationshipId}",
                    email, relationship.CustomerId);
                return relationship;
            }
        }

        _logger.LogInformation(
            "No relationship found with email {Email}",
            email);
        return null;
    }

    public async Task<Relationship> CreateAsync(Relationship relationship)
    {
        relationship.CreatedUtc = DateTimeOffset.UtcNow;
        relationship.UpdatedUtc = DateTimeOffset.UtcNow;

        var response = await _container.CreateItemAsync(
            relationship,
            new PartitionKey(relationship.CustomerId));

        _logger.LogInformation(
            "Created relationship {RelationshipId} with email {Email}",
            relationship.CustomerId, relationship.Email);

        return response.Resource;
    }

    public async Task<Relationship> UpdateAsync(Relationship relationship)
    {
        relationship.UpdatedUtc = DateTimeOffset.UtcNow;

        var response = await _container.ReplaceItemAsync(
            relationship,
            relationship.Id,
            new PartitionKey(relationship.CustomerId),
            new ItemRequestOptions { IfMatchEtag = relationship.ETag });

        _logger.LogInformation(
            "Updated relationship {RelationshipId}",
            relationship.CustomerId);

        return response.Resource;
    }

    public async Task DeleteAsync(string relationshipId)
    {
        await _container.DeleteItemAsync<Relationship>(
            relationshipId,
            new PartitionKey(relationshipId));

        _logger.LogInformation(
            "Deleted relationship {RelationshipId}",
            relationshipId);
    }

    public async Task<IEnumerable<Relationship>> GetByStatusAsync(string status)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.documentType = @documentType AND c.status = @status")
            .WithParameter("@documentType", "Relationship")
            .WithParameter("@status", status);

        var results = new List<Relationship>();
        var iterator = _container.GetItemQueryIterator<Relationship>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        _logger.LogInformation(
            "Retrieved {Count} relationships with status {Status}",
            results.Count, status);

        return results;
    }
}
