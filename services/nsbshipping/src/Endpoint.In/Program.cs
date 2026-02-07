using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using RiskInsure.NsbShipping.Domain.Managers;
using RiskInsure.NsbShipping.Domain.Repositories;
using RiskInsure.NsbShipping.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting NsbShipping Endpoint.In");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .NServiceBusEnvironmentConfiguration("RiskInsure.NsbShipping.Endpoint")
        .ConfigureServices((context, services) =>
        {
            var cosmosConnectionString = context.Configuration.GetConnectionString("CosmosDb")
                ?? throw new InvalidOperationException("CosmosDb connection string not configured");
            var databaseName = "NsbShippingDb";
            var containerName = "NsbShipping";
            var cosmosClient = new CosmosClient(cosmosConnectionString);
            var container = cosmosClient.GetContainer(databaseName, containerName);
            services.AddSingleton(container);
            services.AddSingleton<IInventoryReservationRepository, InventoryReservationRepository>();
            services.AddSingleton<IShipmentRepository, ShipmentRepository>();
            services.AddScoped<IInventoryManager, InventoryManager>();
            services.AddScoped<IShippingManager, ShippingManager>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NsbShipping Endpoint.In terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
