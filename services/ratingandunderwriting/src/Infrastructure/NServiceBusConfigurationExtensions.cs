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
        Action<IConfiguration, EndpointConfiguration, TransportExtensions<RabbitMQTransport>>? configurationAction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);

        return hostBuilder.UseNServiceBus(context =>
        {
            var endpointConfiguration = new EndpointConfiguration(endpointName);
            var environment = context.HostingEnvironment.EnvironmentName;

            Console.WriteLine($"[NServiceBus] Configuring endpoint '{endpointName}' for environment: {environment}");

            TransportExtensions<RabbitMQTransport> routing;
            if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
            {
                routing = ConfigureForProduction(context.Configuration, endpointConfiguration);
            }
            else
            {
                routing = ConfigureForDevelopment(context.Configuration, endpointConfiguration);
            }

            ApplySharedEndpointConfiguration(endpointConfiguration, context.Configuration);

            configurationAction?.Invoke(context.Configuration, endpointConfiguration, routing);

            return endpointConfiguration;
        });
    }

    private static TransportExtensions<RabbitMQTransport> ConfigureForProduction(
        IConfiguration configuration,
        EndpointConfiguration endpointConfiguration)
    {
        var rabbitMqConnectionString = configuration.GetConnectionString("RabbitMQ") ??
                                       configuration["RabbitMQ:ConnectionString"] ??
                                       Environment.GetEnvironmentVariable("RabbitMQ__ConnectionString");

        if (string.IsNullOrWhiteSpace(rabbitMqConnectionString))
        {
            throw new InvalidOperationException(
                "Production requires ConnectionStrings:RabbitMQ or RabbitMQ:ConnectionString in configuration");
        }

        Console.WriteLine("[NServiceBus] Production: using RabbitMQ transport");

        var transportExtensions = endpointConfiguration.UseTransport<RabbitMQTransport>();
        transportExtensions.ConnectionString(rabbitMqConnectionString);

        var cosmosEndpoint = configuration["CosmosDb:Endpoint"] ??
                           Environment.GetEnvironmentVariable("CosmosDb__Endpoint");

        if (!string.IsNullOrWhiteSpace(cosmosEndpoint))
        {
            var persistence = endpointConfiguration.UsePersistence<CosmosPersistence>();
            persistence.CosmosClient(new CosmosClient(cosmosEndpoint, new Azure.Identity.DefaultAzureCredential()));
            persistence.DatabaseName("RiskInsure");
            persistence.DefaultContainer("RatingUnderwriting-Sagas", "/id");
        }

        var license = configuration["NSERVICEBUS_LICENSE"] ??
                     Environment.GetEnvironmentVariable("NSERVICEBUS_LICENSE");
        if (!string.IsNullOrWhiteSpace(license))
        {
            endpointConfiguration.License(license);
        }

        return transportExtensions;
    }

    private static TransportExtensions<RabbitMQTransport> ConfigureForDevelopment(
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
        persistence.DefaultContainer("RatingUnderwriting-Sagas", "/id");

        var recoverability = endpointConfiguration.Recoverability();
        recoverability.Immediate(immediate => immediate.NumberOfRetries(0));
        recoverability.Delayed(delayed => delayed.NumberOfRetries(0));

        Console.WriteLine("[NServiceBus] Development: enabling installers");
        endpointConfiguration.EnableInstallers();

        return transportExtensions;
    }

    private static void ApplySharedEndpointConfiguration(
        EndpointConfiguration endpointConfiguration,
        IConfiguration configuration)
    {
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();

        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.AuditProcessedMessagesTo("audit");
        endpointConfiguration.AuditSagaStateChanges("audit");

        var conventions = endpointConfiguration.Conventions();
        conventions.DefiningEventsAs(type =>
            type.Namespace != null && type.Namespace.EndsWith("Events"));
        conventions.DefiningCommandsAs(type =>
            type.Namespace != null && type.Namespace.EndsWith("Commands"));
        conventions.DefiningMessagesAs(type =>
            type.Namespace != null && type.Namespace.EndsWith("Messages"));
    }
}
