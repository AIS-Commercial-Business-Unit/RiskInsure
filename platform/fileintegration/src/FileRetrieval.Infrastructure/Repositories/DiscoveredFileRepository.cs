using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Repositories;
using RiskInsure.FileRetrieval.Infrastructure.Cosmos;

namespace RiskInsure.FileRetrieval.Infrastructure.Repositories;

/// <summary>
/// T038: Cosmos DB implementation of IDiscoveredFileRepository
/// </summary>
public class DiscoveredFileRepository : IDiscoveredFileRepository
{
    private readonly CosmosDbContext _context;
    private readonly ILogger<DiscoveredFileRepository> _logger;

    public DiscoveredFileRepository(
        CosmosDbContext context,
        ILogger<DiscoveredFileRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DiscoveredFile?> CreateAsync(
        DiscoveredFile discoveredFile,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Creating discovered file record for {FileUrl}",
                discoveredFile.FileUrl);

            var response = await _context.DiscoveredFilesContainer.CreateItemAsync(
                discoveredFile,
                new PartitionKey(discoveredFile.ClientId),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // Unique key constraint violation - file already discovered (idempotency)
            _logger.LogInformation(
                "File {FileUrl} already discovered (idempotency enforced)",
                discoveredFile.FileUrl);
            return null;
        }
    }

    public async Task<bool> ExistsAsync(
        string clientId,
        Guid configurationId,
        string fileUrl,
        DateOnly discoveryDate,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            @"SELECT VALUE COUNT(1) FROM c 
              WHERE c.clientId = @clientId 
                AND c.configurationId = @configurationId
                AND c.fileUrl = @fileUrl
                AND c.discoveryDate = @discoveryDate")
            .WithParameter("@clientId", clientId)
            .WithParameter("@configurationId", configurationId.ToString())
            .WithParameter("@fileUrl", fileUrl)
            .WithParameter("@discoveryDate", discoveryDate.ToString("o"));

        var iterator = _context.DiscoveredFilesContainer.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(clientId) });

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault() > 0;
    }

    public async Task<IReadOnlyList<DiscoveredFile>> GetByExecutionAsync(
        string clientId,
        Guid configurationId,
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            @"SELECT * FROM c 
              WHERE c.clientId = @clientId 
                AND c.configurationId = @configurationId
                AND c.executionId = @executionId")
            .WithParameter("@clientId", clientId)
            .WithParameter("@configurationId", configurationId.ToString())
            .WithParameter("@executionId", executionId.ToString());

        var iterator = _context.DiscoveredFilesContainer.GetItemQueryIterator<DiscoveredFile>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(clientId) });

        var results = new List<DiscoveredFile>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<IReadOnlyList<DiscoveredFile>> GetByConfigurationAsync(
        string clientId,
        Guid configurationId,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.clientId = @clientId AND c.configurationId = @configurationId ORDER BY c.discoveredAt DESC OFFSET 0 LIMIT @pageSize")
            .WithParameter("@clientId", clientId)
            .WithParameter("@configurationId", configurationId.ToString())
            .WithParameter("@pageSize", pageSize);

        var iterator = _context.DiscoveredFilesContainer.GetItemQueryIterator<DiscoveredFile>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(clientId) });

        var results = new List<DiscoveredFile>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<DiscoveredFile> UpdateAsync(
        DiscoveredFile discoveredFile,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating discovered file {FileUrl} status",
            discoveredFile.FileUrl);

        var response = await _context.DiscoveredFilesContainer.ReplaceItemAsync(
            discoveredFile,
            discoveredFile.Id.ToString(),
            new PartitionKey(discoveredFile.ClientId),
            cancellationToken: cancellationToken);

        return response.Resource;
    }
}
