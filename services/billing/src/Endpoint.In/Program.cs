using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RiskInsure.Billing.Domain.Managers;
using RiskInsure.Billing.Domain.Services.BillingDb;
using Serilog;
using RiskInsure.Billing.Infrastructure;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Billing Endpoint.In");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .NServiceBusEnvironmentConfiguration("RiskInsure.Billing.Endpoint",
        (config, endpoint, routing) =>
        {
          // Route commands to Billing Endpoint
          //  routing.RouteToEndpoint(typeof(RecordPayment), "RiskInsure.Billing.Endpoint");
        })
        .ConfigureServices((context, services) =>
        {
            // Application Insights telemetry (auto-reads APPLICATIONINSIGHTS_CONNECTION_STRING env var)
            services.AddApplicationInsightsTelemetryWorkerService();

            // Register Cosmos DB container for billing data (not sagas - sagas configured in NServiceBus persistence)
            var cosmosConnectionString = context.Configuration.GetConnectionString("CosmosDb")
                ?? throw new InvalidOperationException("CosmosDb connection string not configured");

            var databaseName = context.Configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
            var billingContainerName = context.Configuration["CosmosDb:BillingContainerName"] ?? "Billing";

            var cosmosClient = new CosmosClient(cosmosConnectionString);
            var container = cosmosClient.GetContainer(databaseName, billingContainerName);
            services.AddSingleton(container);

            // Register repositories
            services.AddSingleton<IBillingAccountRepository, BillingAccountRepository>();

            // Register managers
            services.AddScoped<IBillingPaymentManager, BillingPaymentManager>();
            services.AddScoped<IBillingAccountManager, BillingAccountManager>();
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
