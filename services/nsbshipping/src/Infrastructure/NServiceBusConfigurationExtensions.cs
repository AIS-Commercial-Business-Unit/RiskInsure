// COPIED AND ADAPTED FROM Billing
namespace RiskInsure.NsbShipping.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using NServiceBus.Persistence.CosmosDB;
using NServiceBus.Transport.AzureServiceBus;

public static class NServiceBusConfigurationExtensions
{
    public static IHostBuilder NServiceBusEnvironmentConfiguration(this IHostBuilder builder, string endpointName)
    {
        builder.UseNServiceBus((context) =>
        {
            var configuration = context.Configuration;
            var environment = context.HostingEnvironment.EnvironmentName;
            var endpointConfiguration = new EndpointConfiguration(endpointName);

            if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
            {
                var fqn = configuration["AzureServiceBus:FullyQualifiedNamespace"];
                var transport = new AzureServiceBusTransport(fqn, new Azure.Identity.DefaultAzureCredential(), TopicTopology.Default);
                endpointConfiguration.UseTransport(transport);
            }
            else
            {
                var connectionString = configuration.GetConnectionString("ServiceBus");
                var transport = new AzureServiceBusTransport(connectionString, TopicTopology.Default);
                endpointConfiguration.UseTransport(transport);
            }

            var cosmosConnectionString = configuration.GetConnectionString("CosmosDb");
            var persistence = endpointConfiguration.UsePersistence<CosmosPersistence>();
            persistence.CosmosClient(new Microsoft.Azure.Cosmos.CosmosClient(cosmosConnectionString));
            persistence.DatabaseName("NsbShippingDb");
            persistence.ContainerName("NsbShipping-Sagas");
            persistence.DefaultContainerPartitionKeyPath("/orderId");

            endpointConfiguration.EnableInstallers();
            endpointConfiguration.AuditProcessedMessagesTo("audit");
            endpointConfiguration.SendFailedMessagesTo("error");
            endpointConfiguration.AuditSagaStateChanges("audit");

            return endpointConfiguration;
        });
        return builder;
    }
}
