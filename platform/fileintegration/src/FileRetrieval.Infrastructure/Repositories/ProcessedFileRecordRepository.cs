using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Repositories;
using RiskInsure.FileRetrieval.Infrastructure.Cosmos;

namespace RiskInsure.FileRetrieval.Infrastructure.Repositories;

/// <summary>
/// Cosmos DB implementation for processed file records.
/// </summary>
public class ProcessedFileRecordRepository : IProcessedFileRecordRepository
{
    private readonly CosmosDbContext _context;
    private readonly ILogger<ProcessedFileRecordRepository> _logger;

    public ProcessedFileRecordRepository(
        CosmosDbContext context,
        ILogger<ProcessedFileRecordRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProcessedFileRecord?> CreateAsync(
        ProcessedFileRecord record,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _context.ProcessedFilesContainer.CreateItemAsync(
                record,
                new PartitionKey(record.ClientId),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogInformation(
                "ProcessedFileRecord already exists for DiscoveredFileId {DiscoveredFileId} (idempotent duplicate)",
                record.DiscoveredFileId);

            return null;
        }
    }

    public async Task<IReadOnlyList<ProcessedFileRecord>> GetByConfigurationAsync(
        string clientId,
        Guid configurationId,
        int pageSize = 50,
        string? fileName = null,
        Guid? executionId = null,
        CancellationToken cancellationToken = default)
    {
        var queryText =
            @"SELECT * FROM c
              WHERE c.clientId = @clientId
                AND c.configurationId = @configurationId";

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            queryText += " AND c.filename = @fileName";
        }

        if (executionId.HasValue)
        {
            queryText += " AND c.executionId = @executionId";
        }

        queryText += " ORDER BY c.processedAt DESC OFFSET 0 LIMIT @pageSize";

        var query = new QueryDefinition(queryText)
            .WithParameter("@clientId", clientId)
            .WithParameter("@configurationId", configurationId.ToString())
            .WithParameter("@pageSize", pageSize);

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            query = query.WithParameter("@fileName", fileName);
        }

        if (executionId.HasValue)
        {
            query = query.WithParameter("@executionId", executionId.Value.ToString());
        }

        var iterator = _context.ProcessedFilesContainer.GetItemQueryIterator<ProcessedFileRecord>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(clientId) });

        var results = new List<ProcessedFileRecord>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }
}
