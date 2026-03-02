using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RiskInsure.FileRetrieval.Infrastructure.Cosmos;

namespace RiskInsure.FileRetrieval.Infrastructure;

/// <summary>
/// Background service to ensure Cosmos DB resources exist and initialize context on startup.
/// </summary>
internal class CosmosDbInitializer : IHostedService
{
    private readonly CosmosDbContext _context;

    public CosmosDbInitializer(CosmosDbContext context)
    {
        _context = context;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _context.InitializeAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
