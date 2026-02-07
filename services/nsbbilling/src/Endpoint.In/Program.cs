using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using RiskInsure.Billing.Domain.Managers;
using RiskInsure.Billing.Domain.Repositories;
using RiskInsure.Billing.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Billing Endpoint.In");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .NServiceBusEnvironmentConfiguration("RiskInsure.Billing.Endpoint")
        .ConfigureServices((context, services) =>
        {
            // Register Cosmos DB container
            var cosmosConnectionString = context.Configuration.GetConnectionString("CosmosDb")
                ?? throw new InvalidOperationException("CosmosDb connection string not configured");

            var databaseName = "BillingDb";
            var containerName = "Billing";

            var cosmosClient = new CosmosClient(cosmosConnectionString);
            var container = cosmosClient.GetContainer(databaseName, containerName);
            services.AddSingleton(container);

            // Register repositories
            services.AddSingleton<IBillingRepository, BillingRepository>();

            // Register managers
            services.AddScoped<IBillingManager, BillingManager>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Billing Endpoint.In terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
