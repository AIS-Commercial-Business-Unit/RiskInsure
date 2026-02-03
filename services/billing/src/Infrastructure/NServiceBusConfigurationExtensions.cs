using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using NServiceBus.Metrics.ServiceControl;
using NServiceBus.Transport.AzureServiceBus;

namespace Infrastructure;

public static class NServiceBusConfigurationExtensions
{
    public static IHostBuilder NServiceBusEnvironmentConfiguration(
        this IHostBuilder hostBuilder,
        string endpointName,
        Action<IConfiguration, EndpointConfiguration, RoutingSettings<AzureServiceBusTransport>>? configurationAction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);

        return hostBuilder.UseNServiceBus(context =>
        {
            var endpointConfiguration = new EndpointConfiguration(endpointName);
            var environment = context.HostingEnvironment.EnvironmentName;

            Console.WriteLine($"[NServiceBus] Configuring endpoint '{endpointName}' for environment: {environment}");

            RoutingSettings<AzureServiceBusTransport> routing;
            if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
            {
                routing = ConfigureForProduction(context.Configuration, endpointConfiguration);
            }
            else
            {
                routing = ConfigureForDevelopment(context.Configuration, endpointConfiguration);
            }

            ApplySharedEndpointConfiguration(endpointConfiguration, context.Configuration);

            // Allow per-endpoint customization
            configurationAction?.Invoke(context.Configuration, endpointConfiguration, routing);

            return endpointConfiguration;
        });
    }

    private static RoutingSettings<AzureServiceBusTransport> ConfigureForProduction(
        IConfiguration configuration,
        EndpointConfiguration endpointConfiguration)
    {
        // Azure Service Bus with Managed Identity
        var fqn = configuration["AzureServiceBus:FullyQualifiedNamespace"] ??
                  Environment.GetEnvironmentVariable("AzureServiceBus__FullyQualifiedNamespace");

        if (string.IsNullOrWhiteSpace(fqn))
        {
            throw new InvalidOperationException(
                "Production requires AzureServiceBus:FullyQualifiedNamespace in configuration");
        }

        Console.WriteLine($"[NServiceBus] Production: using Service Bus namespace {fqn}");

        var transport = new AzureServiceBusTransport(fqn, new DefaultAzureCredential(), TopicTopology.Default);
        var transportExtensions = endpointConfiguration.UseTransport(transport);

        // Cosmos DB persistence with Managed Identity
        var cosmosEndpoint = configuration["CosmosDb:Endpoint"] ??
                           Environment.GetEnvironmentVariable("CosmosDb__Endpoint");

        if (!string.IsNullOrWhiteSpace(cosmosEndpoint))
        {
            var persistence = endpointConfiguration.UsePersistence<CosmosPersistence>();
            persistence.CosmosClient(new CosmosClient(cosmosEndpoint, new Azure.Identity.DefaultAzureCredential()));
            persistence.DatabaseName("RiskInsure");
            persistence.DefaultContainer("Billing-Sagas", "/id");
        }

        // License for production
        var license = configuration["NSERVICEBUS_LICENSE"] ??                     Environment.GetEnvironmentVariable("NSERVICEBUS_LICENSE");
        if (!string.IsNullOrWhiteSpace(license))
        {
            endpointConfiguration.License(license);
        }
        
        return transportExtensions;
    }

    private static RoutingSettings<AzureServiceBusTransport> ConfigureForDevelopment(
        IConfiguration configuration,
        EndpointConfiguration endpointConfiguration)
    {
        // Azure Service Bus connection string
        var serviceBusConnectionString = configuration.GetConnectionString("ServiceBus") ??
                                        configuration["AzureWebJobsServiceBus"];

        if (string.IsNullOrWhiteSpace(serviceBusConnectionString))
        {
            throw new InvalidOperationException(
                "ServiceBus connection string missing. Add ConnectionStrings:ServiceBus to appsettings.Development.json");
        }

        Console.WriteLine($"[NServiceBus] Development: using Service Bus connection string");

        var transport = new AzureServiceBusTransport(serviceBusConnectionString, TopicTopology.Default);
        
        var transportExtensions = endpointConfiguration.UseTransport(transport);

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

        // Enable installers for development
        endpointConfiguration.EnableInstallers();
        
        return transportExtensions;
    }

    private static void ApplySharedEndpointConfiguration(
        EndpointConfiguration endpointConfiguration,
        IConfiguration configuration)
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