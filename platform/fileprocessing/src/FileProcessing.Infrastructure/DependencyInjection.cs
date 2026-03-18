using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Encryption;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Keys;
using AzureKeyVaultEmulator.Aspire.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FileProcessing.Application.Protocols;
using RiskInsure.FileProcessing.Application.Services;
using RiskInsure.FileProcessing.Domain.Repositories;
using RiskInsure.FileProcessing.Infrastructure.Configuration;
using RiskInsure.FileProcessing.Infrastructure.Cosmos;
using RiskInsure.FileProcessing.Infrastructure.Repositories;
using RiskInsure.FileProcessing.Infrastructure.Scheduling;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Runtime.Serialization;
using System.Transactions;

namespace RiskInsure.FileProcessing.Infrastructure;

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
            var serializerOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                Converters =
                {
                    new ProtocolSettingsJsonConverter(),
                    new System.Text.Json.Serialization.JsonStringEnumConverter()
                }
            };

            JsonPathModifierExtensions.AddJsonPathConverters(serializerOptions);

            var cosmosClientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct,
                Serializer = new CosmosSystemTextJsonSerializer(serializerOptions),
                RequestTimeout = TimeSpan.FromSeconds(10),
                MaxRetryAttemptsOnRateLimitedRequests = 3,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(5)
            };

            var vaultUri = configuration["AzureKeyVault:VaultUri"]
                ?? throw new InvalidOperationException("AzureKeyVault:VaultUri configuration is missing");
            var usingAzureKeyVaultEmulator = configuration["AzureKeyVault:UsingEmulator"] != null &&
                                            bool.TryParse(configuration["AzureKeyVault:UsingEmulator"], out var usingEmulator) &&
                                            usingEmulator;

            var keyResolver = usingAzureKeyVaultEmulator
                ? new KeyResolver(new EmulatedTokenCredential(vaultUri))
                : new KeyResolver(new DefaultAzureCredential());

            return new CosmosClient(connectionString, cosmosClientOptions)
                .WithEncryption(keyResolver, KeyEncryptionKeyResolverName.AzureKeyVault);
        });

        // Cosmos DB Encryption Configuration (singleton)
        services.AddSingleton<CosmosEncryptionConfiguration>();

        // Cosmos DB Context (singleton)
        services.AddSingleton<CosmosDbContext>();

        // Initialize Cosmos DB context on startup
        services.AddHostedService<CosmosDbInitializer>();

        // Repositories (scoped)
        services.AddScoped<IFileProcessingConfigurationRepository, FileProcessingConfigurationRepository>();
        services.AddScoped<IFileProcessingExecutionRepository, FileProcessingExecutionRepository>();
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
        services.AddSingleton<FileProcessingMetricsService>();

        // HTTP client factory for protocol adapters
        services.AddHttpClient();

        // Azure Key Vault SecretClient
        // This stores secrets in Key Vault and retrieves them.  This secret is
        // used for encryption operations in Cosmos DB.  CosmosDB encrypts values
        // used by protocol adapters (FTP/HTTP/BLOB Storage) that are needed 
        // at runtime to retrieve files, but that we don't want stored in 
        // plaintext in CosmosDB.
        var vaultUri = configuration["AzureKeyVault:VaultUri"]
            ?? throw new InvalidOperationException("AzureKeyVault:VaultUri configuration is missing");
        var usingAzureKeyVaultEmulator = configuration["AzureKeyVault:UsingEmulator"] != null &&
                                        bool.TryParse(configuration["AzureKeyVault:UsingEmulator"], out var usingEmulator) &&
                                        usingEmulator;

        if (usingAzureKeyVaultEmulator)
        {
            services.AddAzureKeyVaultEmulator(vaultUri, secrets: true, keys: true, certificates: true);

            var credential = new EmulatedTokenCredential(vaultUri);
            services.AddSingleton(new KeyResolver(credential));
        }
        else
        {
            services.AddSingleton(new SecretClient(new Uri(vaultUri), new DefaultAzureCredential()));            
            services.AddSingleton(new KeyClient(new Uri(vaultUri), new DefaultAzureCredential()));
            services.AddSingleton(new CertificateClient(new Uri(vaultUri), new DefaultAzureCredential()));            
            services.AddSingleton(new KeyResolver(new DefaultAzureCredential()));
        }

        // Configuration Options
        services.Configure<SchedulerOptions>(configuration.GetSection(SchedulerOptions.SectionName));
        
        // Scheduling Services
        services.AddSingleton<ScheduleEvaluator>();

        return services;
    }
}
