using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using NServiceBus.Transport.RabbitMQ;

namespace Infrastructure;

public static class NServiceBusConfigurationExtensions
{
    public static IHostBuilder NServiceBusEnvironmentConfiguration(
        this IHostBuilder hostBuilder,
        string endpointName,
        Action<IConfiguration, EndpointConfiguration, RoutingSettings<RabbitMQTransport>>? configurationAction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);

        return hostBuilder.UseNServiceBus(context =>
        {
            var endpointConfiguration = new EndpointConfiguration(endpointName);
            var environment = context.HostingEnvironment.EnvironmentName;

            Console.WriteLine($"[NServiceBus] Configuring endpoint '{endpointName}' for environment: {environment}");

            RoutingSettings<RabbitMQTransport> routing;
            if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
            {
                routing = ConfigureForProduction(context.Configuration, endpointConfiguration);
            }
            else
            {
                routing = ConfigureForDevelopment(context.Configuration, endpointConfiguration);
            }

            ApplySharedEndpointConfiguration(endpointConfiguration);

            // Allow per-endpoint customization
            configurationAction?.Invoke(context.Configuration, endpointConfiguration, routing);

            return endpointConfiguration;
        });
    }

    private static RoutingSettings<RabbitMQTransport> ConfigureForProduction(
        IConfiguration configuration,
        EndpointConfiguration endpointConfiguration)
    {
        var rabbitMqConnectionString = configuration.GetConnectionString("RabbitMQ");

        if (string.IsNullOrWhiteSpace(rabbitMqConnectionString))
        {
            throw new InvalidOperationException(
                "Production requires ConnectionStrings:RabbitMQ or RabbitMQ:ConnectionString in configuration");
        }

        Console.WriteLine("[NServiceBus] Production: using RabbitMQ transport");

        var transportExtensions = endpointConfiguration.UseTransport<RabbitMQTransport>();
        transportExtensions.ConnectionString(rabbitMqConnectionString);

        // Cosmos DB persistence with Managed Identity
        var cosmosEndpoint = configuration["CosmosDb:Endpoint"] ??
                           Environment.GetEnvironmentVariable("CosmosDb__Endpoint");

        if (!string.IsNullOrWhiteSpace(cosmosEndpoint))
        {
            var persistence = endpointConfiguration.UsePersistence<CosmosPersistence>();
            persistence.CosmosClient(new CosmosClient(cosmosEndpoint, new DefaultAzureCredential()));
            persistence.DatabaseName("RiskInsure");
            persistence.DefaultContainer("Billing-Sagas", "/id");
        }

        // License for production
        var license = configuration["NSERVICEBUS_LICENSE"] ?? Environment.GetEnvironmentVariable("NSERVICEBUS_LICENSE");
        if (!string.IsNullOrWhiteSpace(license))
        {
            endpointConfiguration.License(license);
        }
        
        return transportExtensions.Routing();
    }

    private static RoutingSettings<RabbitMQTransport> ConfigureForDevelopment(
        IConfiguration configuration,
        EndpointConfiguration endpointConfiguration)
    {
        var rabbitMqConnectionString = configuration.GetConnectionString("RabbitMQ") ??
                                       configuration["RabbitMQ:ConnectionString"];

        if (string.IsNullOrWhiteSpace(rabbitMqConnectionString))
        {
            throw new InvalidOperationException(
                "RabbitMQ connection string missing. Add ConnectionStrings:RabbitMQ to appsettings.Development.json");
        }

        Console.WriteLine("[NServiceBus] Development: using RabbitMQ transport");

        var transportExtensions = endpointConfiguration.UseTransport<RabbitMQTransport>()
            .UseConventionalRoutingTopology(QueueType.Classic);
        transportExtensions.ConnectionString(rabbitMqConnectionString);

        // Cosmos DB connection string
        var cosmosConnectionString = configuration.GetConnectionString("CosmosDb") ??
                                    configuration["CosmosDbConnectionString"];

        if (string.IsNullOrWhiteSpace(cosmosConnectionString))
        {
            throw new InvalidOperationException(
                "CosmosDb connection string missing. Add ConnectionStrings:CosmosDb to appsettings.Development.json");
        }

        var persistence = endpointConfiguration.UsePersistence<CosmosPersistence>();
        persistence.CosmosClient(new CosmosClient(cosmosConnectionString));
        persistence.DatabaseName("RiskInsure");
        persistence.DefaultContainer("Billing-Sagas", "/id");

        // Disable retries for faster dev cycle
        var recoverability = endpointConfiguration.Recoverability();
        recoverability.Immediate(immediate => immediate.NumberOfRetries(0));
        recoverability.Delayed(delayed => delayed.NumberOfRetries(0));

        Console.WriteLine("[NServiceBus] Development: enabling installers");
        endpointConfiguration.EnableInstallers();

        return transportExtensions.Routing();
    }

    private static void ApplySharedEndpointConfiguration(
        EndpointConfiguration endpointConfiguration)
    {
        // Serialization
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();

        // Error and audit queues
        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.AuditProcessedMessagesTo("audit");
        endpointConfiguration.AuditSagaStateChanges("audit");

        // ServiceControl metrics - disabled for send-only endpoints
        // Note: Send-only endpoints (API) cannot send metrics
        // Only enable for full endpoints (Endpoint.In) if ServiceControl is running
        // var metrics = endpointConfiguration.EnableMetrics();
        // metrics.SendMetricDataToServiceControl("particular.monitoring", TimeSpan.FromSeconds(10));

        // Message conventions (namespace-based)
        var conventions = endpointConfiguration.Conventions();
        conventions.DefiningEventsAs(type =>
            type.Namespace != null && type.Namespace.EndsWith("Events"));
        conventions.DefiningCommandsAs(type =>
            type.Namespace != null && type.Namespace.EndsWith("Commands"));
        conventions.DefiningMessagesAs(type =>
            type.Namespace != null && type.Namespace.EndsWith("Messages"));
    }
}