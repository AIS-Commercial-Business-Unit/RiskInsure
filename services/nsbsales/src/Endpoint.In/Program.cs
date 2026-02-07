using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using RiskInsure.NsbSales.Domain.Managers;
using RiskInsure.NsbSales.Domain.Repositories;
using RiskInsure.NsbSales.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting NsbSales Endpoint.In");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .NServiceBusEnvironmentConfiguration("RiskInsure.NsbSales.Endpoint")
        .ConfigureServices((context, services) =>
        {
            // Register Cosmos DB container
            var cosmosConnectionString = context.Configuration.GetConnectionString("CosmosDb")
                ?? throw new InvalidOperationException("CosmosDb connection string not configured");

            var databaseName = "NsbSalesDb";
            var containerName = "Orders";

            var cosmosClient = new CosmosClient(cosmosConnectionString);
            var container = cosmosClient.GetContainer(databaseName, containerName);
            services.AddSingleton(container);

            // Register repositories
            services.AddSingleton<IOrderRepository, OrderRepository>();

            // Register managers
            services.AddScoped<IOrderManager, OrderManager>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NsbSales Endpoint.In terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
