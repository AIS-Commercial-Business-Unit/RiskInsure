using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.Repositories;
using RiskInsure.FileRetrieval.Infrastructure.Cosmos;

namespace RiskInsure.FileRetrieval.Infrastructure.Repositories;

/// <summary>
/// T037: Cosmos DB implementation of IFileRetrievalExecutionRepository
/// </summary>
public class FileRetrievalExecutionRepository : IFileRetrievalExecutionRepository
{
    private readonly CosmosDbContext _context;
    private readonly ILogger<FileRetrievalExecutionRepository> _logger;

    public FileRetrievalExecutionRepository(
        CosmosDbContext context,
        ILogger<FileRetrievalExecutionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<FileRetrievalExecution> CreateAsync(
        FileRetrievalExecution execution,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating execution {ExecutionId} for configuration {ConfigurationId}",
            execution.Id,
            execution.ConfigurationId);

        // Partition key is clientId for hierarchical partitioning
        var response = await _context.ExecutionsContainer.CreateItemAsync(
            execution,
            new PartitionKey(execution.ClientId),
            cancellationToken: cancellationToken);

        return response.Resource;
    }

    public async Task<FileRetrievalExecution?> GetByIdAsync(
        string clientId,
        Guid configurationId,
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _context.ExecutionsContainer.ReadItemAsync<FileRetrievalExecution>(
                executionId.ToString(),
                new PartitionKey(clientId),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<FileRetrievalExecution>> GetByConfigurationAsync(
        string clientId,
        Guid configurationId,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.clientId = @clientId AND c.configurationId = @configurationId ORDER BY c.executionStartedAt DESC OFFSET 0 LIMIT @pageSize")
            .WithParameter("@clientId", clientId)
            .WithParameter("@configurationId", configurationId.ToString())
            .WithParameter("@pageSize", pageSize);

        var iterator = _context.ExecutionsContainer.GetItemQueryIterator<FileRetrievalExecution>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(clientId) });

        var results = new List<FileRetrievalExecution>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<IReadOnlyList<FileRetrievalExecution>> GetByConfigurationAndDateRangeAsync(
        string clientId,
        Guid configurationId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            @"SELECT * FROM c 
              WHERE c.clientId = @clientId 
                AND c.configurationId = @configurationId
                AND c.executionStartedAt >= @startDate 
                AND c.executionStartedAt <= @endDate 
              ORDER BY c.executionStartedAt DESC")
            .WithParameter("@clientId", clientId)
            .WithParameter("@configurationId", configurationId.ToString())
            .WithParameter("@startDate", startDate.ToString("o"))
            .WithParameter("@endDate", endDate.ToString("o"));

        var iterator = _context.ExecutionsContainer.GetItemQueryIterator<FileRetrievalExecution>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(clientId) });

        var results = new List<FileRetrievalExecution>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<(IReadOnlyList<FileRetrievalExecution> Executions, string? ContinuationToken)> GetExecutionHistoryAsync(
        string clientId,
        Guid configurationId,
        int pageSize = 50,
        string? continuationToken = null,
        ExecutionStatus? statusFilter = null,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Getting execution history for configuration {ConfigurationId} (pageSize: {PageSize}, status: {Status}, dateRange: {StartDate} to {EndDate})",
            configurationId,
            pageSize,
            statusFilter,
            startDate,
            endDate);

        // Build dynamic query based on filters
        var queryText = "SELECT * FROM c WHERE c.clientId = @clientId AND c.configurationId = @configurationId";
        var queryDef = new QueryDefinition(queryText)
            .WithParameter("@clientId", clientId)
            .WithParameter("@configurationId", configurationId.ToString());

        if (statusFilter.HasValue)
        {
            queryText += " AND c.status = @status";
            queryDef = queryDef.WithParameter("@status", statusFilter.Value.ToString());
        }

        if (startDate.HasValue)
        {
            queryText += " AND c.executionStartedAt >= @startDate";
            queryDef = queryDef.WithParameter("@startDate", startDate.Value.ToString("o"));
        }

        if (endDate.HasValue)
        {
            queryText += " AND c.executionStartedAt <= @endDate";
            queryDef = queryDef.WithParameter("@endDate", endDate.Value.ToString("o"));
        }

        // Add ordering for consistent pagination
        queryText += " ORDER BY c.executionStartedAt DESC";
        
        // Rebuild query with all parameters
        queryDef = new QueryDefinition(queryText)
            .WithParameter("@clientId", clientId)
            .WithParameter("@configurationId", configurationId.ToString());
        
        if (statusFilter.HasValue)
        {
            queryDef = queryDef.WithParameter("@status", statusFilter.Value.ToString());
        }
        if (startDate.HasValue)
        {
            queryDef = queryDef.WithParameter("@startDate", startDate.Value.ToString("o"));
        }
        if (endDate.HasValue)
        {
            queryDef = queryDef.WithParameter("@endDate", endDate.Value.ToString("o"));
        }

        var iterator = _context.ExecutionsContainer.GetItemQueryIterator<FileRetrievalExecution>(
            queryDef,
            continuationToken: continuationToken,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(clientId),
                MaxItemCount = pageSize
            });

        var results = new List<FileRetrievalExecution>();
        string? nextToken = null;

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
            nextToken = response.ContinuationToken;
        }

        _logger.LogDebug(
            "Retrieved {Count} executions for configuration {ConfigurationId} (hasMore: {HasMore})",
            results.Count,
            configurationId,
            !string.IsNullOrEmpty(nextToken));

        return (results.AsReadOnly(), nextToken);
    }

    public async Task<FileRetrievalExecution> UpdateAsync(
        FileRetrievalExecution execution,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating execution {ExecutionId} status",
            execution.Id);

        var response = await _context.ExecutionsContainer.ReplaceItemAsync(
            execution,
            execution.Id.ToString(),
            new PartitionKey(execution.ClientId),
            cancellationToken: cancellationToken);

        return response.Resource;
    }
}
