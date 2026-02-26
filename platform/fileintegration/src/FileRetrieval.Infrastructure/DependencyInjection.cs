using Microsoft.Azure.Cosmos;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FileRetrieval.Application.Protocols;
using RiskInsure.FileRetrieval.Application.Services;
using RiskInsure.FileRetrieval.Domain.Repositories;
using RiskInsure.FileRetrieval.Infrastructure.Configuration;
using RiskInsure.FileRetrieval.Infrastructure.Cosmos;
using RiskInsure.FileRetrieval.Infrastructure.KeyVault;
using RiskInsure.FileRetrieval.Infrastructure.Repositories;
using RiskInsure.FileRetrieval.Infrastructure.Scheduling;

namespace RiskInsure.FileRetrieval.Infrastructure;

/// <summary>
/// T040: Dependency injection configuration for Infrastructure layer
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Cosmos DB Client (singleton)
        services.AddSingleton<CosmosClient>(sp =>
        {
            var cosmosEndpoint = configuration["CosmosDb:EndpointUri"]
                ?? throw new InvalidOperationException("CosmosDb:EndpointUri configuration is missing");

            return new CosmosClient(
                cosmosEndpoint,
                new Azure.Identity.DefaultAzureCredential(),
                new CosmosClientOptions
                {
                    SerializerOptions = new CosmosSerializationOptions
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                    }
                });
        });

        // Cosmos DB Context (singleton)
        services.AddSingleton<CosmosDbContext>();

        // Initialize Cosmos DB context on startup
        services.AddHostedService<CosmosDbInitializer>();

        // Repositories (scoped)
        services.AddScoped<IFileRetrievalConfigurationRepository, FileRetrievalConfigurationRepository>();
        services.AddScoped<IFileRetrievalExecutionRepository, FileRetrievalExecutionRepository>();
        services.AddScoped<IDiscoveredFileRepository, DiscoveredFileRepository>();

        // Application services (scoped)
        services.AddScoped<ConfigurationService>();
        services.AddScoped<ExecutionHistoryService>();
        services.AddScoped<FileCheckService>();
        services.AddScoped<ProtocolAdapterFactory>();

        // Stateless utility/metrics services (singletons)
        services.AddSingleton<TokenReplacementService>();
        services.AddSingleton<FileRetrievalMetricsService>();

        // HTTP client factory for protocol adapters
        services.AddHttpClient();

        // Azure Key Vault SecretClient (for protocol adapters)
        services.AddSingleton<SecretClient>(_ =>
        {
            var vaultUri = configuration["AzureKeyVault:VaultUri"]
                ?? throw new InvalidOperationException("AzureKeyVault:VaultUri configuration is missing");

            return new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
        });

        // Key Vault (singleton)
        services.AddSingleton<KeyVaultSecretClient>();

        // Configuration Options
        services.Configure<SchedulerOptions>(configuration.GetSection(SchedulerOptions.SectionName));
        
        // Scheduling Services
        services.AddSingleton<ScheduleEvaluator>();

        return services;
    }
}

/// <summary>
/// Background service to initialize Cosmos DB context on startup
/// </summary>
internal class CosmosDbInitializer : Microsoft.Extensions.Hosting.IHostedService
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
