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
            var connectionString = configuration.GetConnectionString("CosmosDb") ??
                                throw new InvalidOperationException("CosmosDb connection string is required");

            // Configure CosmosClient to use System.Text.Json serialization
            var cosmosClientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct,
                Serializer = new CosmosSystemTextJsonSerializer(new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    Converters =
                    {
                        new ProtocolSettingsJsonConverter(),
                        new System.Text.Json.Serialization.JsonStringEnumConverter()
                    }
                }),
                RequestTimeout = TimeSpan.FromSeconds(10),
                MaxRetryAttemptsOnRateLimitedRequests = 3,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(5)
            };

            return new CosmosClient(connectionString, cosmosClientOptions);
        });

        // Cosmos DB Encryption Configuration (singleton)
        services.AddSingleton<CosmosEncryptionConfiguration>();

        // Cosmos DB Context (singleton)
        services.AddSingleton<CosmosDbContext>();

        // Initialize Cosmos DB context on startup
        services.AddHostedService<CosmosDbInitializer>();

        // Repositories (scoped)
        services.AddScoped<IFileRetrievalConfigurationRepository, FileRetrievalConfigurationRepository>();
        services.AddScoped<IFileRetrievalExecutionRepository, FileRetrievalExecutionRepository>();
        services.AddScoped<IDiscoveredFileRepository, DiscoveredFileRepository>();
        services.AddScoped<IProcessedFileRecordRepository, ProcessedFileRecordRepository>();

        // Application services (scoped)
        services.AddScoped<ConfigurationService>();
        services.AddScoped<ExecutionHistoryService>();
        services.AddScoped<FileCheckService>();
        services.AddScoped<DiscoveredFileContentDownloadService>();
        services.AddScoped<ProtocolAdapterFactory>();

        // Stateless utility/metrics services (singletons)
        services.AddSingleton<TokenReplacementService>();
        services.AddSingleton<FileRetrievalMetricsService>();

        // HTTP client factory for protocol adapters
        services.AddHttpClient();

        // Azure Key Vault SecretClient
        // This stores secrets in Key Vault and retrieves them.  This secret is
        // used for encryption operations in Cosmos DB.  CosmosDB encrypts values
        // used by protocol adapters (FTP/HTTP/BLOB Storage) that are needed 
        // at runtime to retrieve files, but that we don't want stored in 
        // plaintext in CosmosDB.
        services.AddSingleton<SecretClient>(_ =>
        {
            var keyVaultUri = configuration["AzureKeyVault:VaultUri"]
                ?? throw new InvalidOperationException("AzureKeyVault:VaultUri configuration is missing");

            return new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential());
        });

        // Configuration Options
        services.Configure<SchedulerOptions>(configuration.GetSection(SchedulerOptions.SectionName));
        
        // Scheduling Services
        services.AddSingleton<ScheduleEvaluator>();

        return services;
    }
}
