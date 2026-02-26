using System.Configuration;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NServiceBus.Features;

namespace RiskInsure.FundTransferMgt.Infrastructure;

public static class NServiceBusConfigurationExtensions
{
    public static IHostBuilder NServiceBusEnvironmentConfiguration(
        this IHostBuilder hostBuilder,
        string endpointName,
        Action<IConfiguration, EndpointConfiguration, RoutingSettings>? configurationAction = null,
        bool isSendOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);

        return hostBuilder.UseNServiceBus(context =>
        {
            var endpointConfiguration = new EndpointConfiguration(endpointName);
            var environment = context.HostingEnvironment.EnvironmentName;

            Console.WriteLine($"[NServiceBus] Configuring endpoint '{endpointName}' for environment: {environment}");

            endpointConfiguration
                .ApplyNServiceBusLicense(context.Configuration)
                .PersistWithCosmosDb(context.Configuration)
                .ApplySharedEndpointConfiguration();

            // Heartbeats, metrics, and custom checks only apply to full endpoints
            // Send-only endpoints (APIs) cannot receive messages so these features are not supported
            if (!isSendOnly)
            {
                endpointConfiguration.ConfigureServicePlatformConnection();
            }

            RoutingSettings routing;
            var messageBroker = context.Configuration["Messaging:MessageBroker"];
            switch (messageBroker)
            {
                case "AzureServiceBus":
                    Console.WriteLine($"[NServiceBus] Messaging: using Azure Service Bus transport based on configuration");
                    routing = ConfigureAzureServiceBus(context.Configuration, endpointConfiguration);
                    break;
                case "RabbitMQ":
                    Console.WriteLine($"[NServiceBus] Messaging: using RabbitMQ transport based on configuration");
                    routing = ConfigureRabbitMQ(context.Configuration, endpointConfiguration);
                    break;
                default:
                    throw new ConfigurationErrorsException(
                        "Invalid or missing Messaging:MessageBroker configuration. " +
                        "Please specify a message broker (e.g. AzureServiceBus, RabbitMQ) in configuration.");
            }

            if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                // For development, we want to disable retries for faster feedback loop. 
                // In production, we want retries enabled for resiliency.
                Console.WriteLine("[NServiceBus]: Disabling recovery (retries)");
                endpointConfiguration.DisableRecovery();

                // Installers are useful in development to automatically create queues and topics, 
                // but in production we should use infrastructure-as-code (e.g. ARM templates, 
                // Terraform) to manage these resources explicitly.  But since the Azure ServiceBus
                // emulator does not support installers, we only turn this on when we're 
                // not using the Service Bus Emulator.
                Console.WriteLine("[NServiceBus]: Enabling installers");
                endpointConfiguration.EnableInstallers();
            }

            // Allow per-endpoint customization
            configurationAction?.Invoke(context.Configuration, endpointConfiguration, routing);

            return endpointConfiguration;
        });
    }

    private static RoutingSettings<AzureServiceBusTransport> ConfigureAzureServiceBus(
        IConfiguration configuration,
        EndpointConfiguration endpointConfiguration)
    {
        var serviceBusConnectionString = configuration.GetConnectionString("ServiceBus");
        if (string.IsNullOrWhiteSpace(serviceBusConnectionString))
        {
            throw new InvalidOperationException(
                "ServiceBus connection string missing. Add ConnectionStrings:ServiceBus to configuration");
        }
        if (serviceBusConnectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigurationErrorsException(
                "The Azure Service Bus emulator does not support all features required by this application (e.g. auto-subscribe). " +
                "For local development, please use RabbitMQ as the message broker. " +
                "To use RabbitMQ, set Messaging:MessageBroker=RabbitMQ in configuration and provide a RabbitMQ connection string.");            
        }
        Console.WriteLine($"[NServiceBus]: using Service Bus connection string");
        
        var transport = new AzureServiceBusTransport(serviceBusConnectionString, TopicTopology.Default);
        var transportExtensions = endpointConfiguration.UseTransport(transport);
        return transportExtensions;
    }

    private static RoutingSettings<RabbitMQTransport> ConfigureRabbitMQ(
        IConfiguration configuration,
        EndpointConfiguration endpointConfiguration)
    {
        var rabbitMqConnectionString = configuration.GetConnectionString("RabbitMQ");
        if (string.IsNullOrWhiteSpace(rabbitMqConnectionString))
        {
            throw new InvalidOperationException(
                "Production requires ConnectionStrings:RabbitMQ or RabbitMQ:ConnectionString in configuration");
        }

        Console.WriteLine("[NServiceBus]: using RabbitMQ transport");

        var transportExtensions = endpointConfiguration.UseTransport<RabbitMQTransport>()
            .UseConventionalRoutingTopology(QueueType.Classic);
        transportExtensions.ConnectionString(rabbitMqConnectionString);
        
        return transportExtensions.Routing();
    }

    private static EndpointConfiguration ApplySharedEndpointConfiguration(
        this EndpointConfiguration endpointConfiguration)
    {
        // Serialization
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();

        // Error and audit queues
        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.AuditProcessedMessagesTo("audit");
        endpointConfiguration.AuditSagaStateChanges("audit");

        // Message conventions (namespace-based)
        var conventions = endpointConfiguration.Conventions();
        conventions.DefiningEventsAs(type =>
            type.Namespace != null && type.Namespace.EndsWith("Events"));
        conventions.DefiningCommandsAs(type =>
            type.Namespace != null && type.Namespace.EndsWith("Commands"));
        conventions.DefiningMessagesAs(type =>
            type.Namespace != null && type.Namespace.EndsWith("Messages"));

        return endpointConfiguration;
    }

    /// <summary>
    /// Configures the ServicePlatform connection for full (non-send-only) endpoints.
    /// Enables heartbeats, metrics, and custom checks so the endpoint is visible
    /// in ServicePulse for monitoring and health tracking.
    /// </summary>
    private static void ConfigureServicePlatformConnection(
        this EndpointConfiguration endpointConfiguration)
    {
        // Heartbeats - sends periodic heartbeat messages to ServiceControl
        // so ServicePulse can detect when an endpoint is offline
        endpointConfiguration.SendHeartbeatTo(
            serviceControlQueue: "Particular.ServiceControl",
            frequency: TimeSpan.FromSeconds(10),
            timeToLive: TimeSpan.FromSeconds(40));

        // Metrics - sends performance data (processing time, throughput, etc.)
        // to the monitoring instance for the ServicePulse Monitoring tab
        var metrics = endpointConfiguration.EnableMetrics();
        metrics.SendMetricDataToServiceControl(
            serviceControlMetricsAddress: "Particular.Monitoring",
            interval: TimeSpan.FromSeconds(10));

        Console.WriteLine("[NServiceBus]: ServicePlatform connection configured (heartbeats, metrics)");
    }

    private static EndpointConfiguration PersistWithCosmosDb(this 
    EndpointConfiguration endpointConfiguration, IConfiguration configuration)
    {
        var cosmosConnectionString = configuration["ConnectionStrings:CosmosDb"];
        if (string.IsNullOrWhiteSpace(cosmosConnectionString))
        {
            throw new InvalidOperationException(
                "CosmosDb endpoint missing. Add ConnectionStrings:CosmosDb to configuration");
        }

        Console.WriteLine($"[NServiceBus]: using Cosmos DB connection string: {cosmosConnectionString}");

        var persistence = endpointConfiguration.UsePersistence<CosmosPersistence>();
        persistence.CosmosClient(new CosmosClient(cosmosConnectionString));
        // Todo: Configuration item
        persistence.DatabaseName("RiskInsure");
        // Todo: Configuration item
        persistence.DefaultContainer("Billing-Sagas", "/id");

        return endpointConfiguration;
    }

    private static EndpointConfiguration ApplyNServiceBusLicense(this 
        EndpointConfiguration endpointConfiguration, IConfiguration configuration)
    {
        // License for production
        var license = configuration["NSERVICEBUS_LICENSE"] 
            ?? Environment.GetEnvironmentVariable("NSERVICEBUS_LICENSE");
        if (!string.IsNullOrWhiteSpace(license))
        {
            endpointConfiguration.License(license);
        }

        return endpointConfiguration;
    }

    /// For development, we want to disable retries for faster feedback loop. In 
    /// production, we want retries enabled for resiliency. 
    private static EndpointConfiguration DisableRecovery(this EndpointConfiguration endpointConfiguration)
    {
        var recoverability = endpointConfiguration.Recoverability();
        recoverability.Immediate(immediate => immediate.NumberOfRetries(0));
        recoverability.Delayed(delayed => delayed.NumberOfRetries(0));

        return endpointConfiguration;
    }
}