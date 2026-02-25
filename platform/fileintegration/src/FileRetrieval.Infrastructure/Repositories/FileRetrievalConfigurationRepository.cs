using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.Repositories;
using RiskInsure.FileRetrieval.Infrastructure.Cosmos;

namespace RiskInsure.FileRetrieval.Infrastructure.Repositories;

/// <summary>
/// T036: Cosmos DB implementation of IFileRetrievalConfigurationRepository
/// </summary>
public class FileRetrievalConfigurationRepository : IFileRetrievalConfigurationRepository
{
    private readonly CosmosDbContext _context;
    private readonly ILogger<FileRetrievalConfigurationRepository> _logger;

    public FileRetrievalConfigurationRepository(
        CosmosDbContext context,
        ILogger<FileRetrievalConfigurationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<FileRetrievalConfiguration> CreateAsync(
        FileRetrievalConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating configuration {ConfigurationId} for client {ClientId}",
            configuration.Id,
            configuration.ClientId);

        var response = await _context.ConfigurationsContainer.CreateItemAsync(
            configuration,
            new PartitionKey(configuration.ClientId),
            cancellationToken: cancellationToken);

        return response.Resource;
    }

    public async Task<FileRetrievalConfiguration?> GetByIdAsync(
        string clientId,
        Guid configurationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _context.ConfigurationsContainer.ReadItemAsync<FileRetrievalConfiguration>(
                configurationId.ToString(),
                new PartitionKey(clientId),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<FileRetrievalConfiguration>> GetByClientAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.clientId = @clientId")
            .WithParameter("@clientId", clientId);

        var iterator = _context.ConfigurationsContainer.GetItemQueryIterator<FileRetrievalConfiguration>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(clientId) });

        var results = new List<FileRetrievalConfiguration>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<(IReadOnlyList<FileRetrievalConfiguration> Configurations, string? ContinuationToken)> GetByClientWithPaginationAsync(
        string clientId,
        int pageSize = 20,
        string? continuationToken = null,
        ProtocolType? protocolFilter = null,
        bool? isActiveFilter = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Getting configurations for client {ClientId} with pagination (pageSize: {PageSize}, protocol: {Protocol}, isActive: {IsActive})",
            clientId,
            pageSize,
            protocolFilter,
            isActiveFilter);

        // Build dynamic query based on filters
        var queryText = "SELECT * FROM c WHERE c.clientId = @clientId";
        var queryDef = new QueryDefinition(queryText).WithParameter("@clientId", clientId);

        if (protocolFilter.HasValue)
        {
            queryText += " AND c.protocol = @protocol";
            queryDef = queryDef.WithParameter("@protocol", protocolFilter.Value.ToString());
        }

        if (isActiveFilter.HasValue)
        {
            queryText += " AND c.isActive = @isActive";
            queryDef = queryDef.WithParameter("@isActive", isActiveFilter.Value);
        }

        // Add ordering for consistent pagination
        queryText += " ORDER BY c.createdAt DESC";
        queryDef = new QueryDefinition(queryText);
        
        // Re-add parameters (QueryDefinition is immutable)
        queryDef = queryDef.WithParameter("@clientId", clientId);
        if (protocolFilter.HasValue)
        {
            queryDef = queryDef.WithParameter("@protocol", protocolFilter.Value.ToString());
        }
        if (isActiveFilter.HasValue)
        {
            queryDef = queryDef.WithParameter("@isActive", isActiveFilter.Value);
        }

        var iterator = _context.ConfigurationsContainer.GetItemQueryIterator<FileRetrievalConfiguration>(
            queryDef,
            continuationToken: continuationToken,
            requestOptions: new QueryRequestOptions 
            { 
                PartitionKey = new PartitionKey(clientId),
                MaxItemCount = pageSize
            });

        var results = new List<FileRetrievalConfiguration>();
        string? nextToken = null;

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
            nextToken = response.ContinuationToken;
        }

        _logger.LogDebug(
            "Retrieved {Count} configurations for client {ClientId} (hasMore: {HasMore})",
            results.Count,
            clientId,
            !string.IsNullOrEmpty(nextToken));

        return (results.AsReadOnly(), nextToken);
    }

    public async Task<IReadOnlyList<FileRetrievalConfiguration>> GetActiveByClientAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.clientId = @clientId AND c.isActive = true")
            .WithParameter("@clientId", clientId);

        var iterator = _context.ConfigurationsContainer.GetItemQueryIterator<FileRetrievalConfiguration>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(clientId) });

        var results = new List<FileRetrievalConfiguration>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<IReadOnlyList<FileRetrievalConfiguration>> GetAllActiveConfigurationsAsync(
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.isActive = true");

        var iterator = _context.ConfigurationsContainer.GetItemQueryIterator<FileRetrievalConfiguration>(query);

        var results = new List<FileRetrievalConfiguration>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<FileRetrievalConfiguration> UpdateAsync(
        FileRetrievalConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating configuration {ConfigurationId} for client {ClientId}",
            configuration.Id,
            configuration.ClientId);

        var response = await _context.ConfigurationsContainer.ReplaceItemAsync(
            configuration,
            configuration.Id.ToString(),
            new PartitionKey(configuration.ClientId),
            new ItemRequestOptions { IfMatchEtag = configuration.ETag },
            cancellationToken: cancellationToken);

        return response.Resource;
    }

    public async Task DeleteAsync(
        string clientId,
        Guid configurationId,
        string etag,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Soft-deleting configuration {ConfigurationId} for client {ClientId}",
            configurationId,
            clientId);

        // Soft delete: set IsActive = false
        var configuration = await GetByIdAsync(clientId, configurationId, cancellationToken);
        if (configuration != null)
        {
            configuration.IsActive = false;
            await UpdateAsync(configuration, cancellationToken);
        }
    }
}
