using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Managers;
using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Services.PolicyEquityAndInvoicingDb;
using Serilog;
using RiskInsure.PolicyEquityAndInvoicingMgt.Infrastructure;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using Microsoft.ApplicationInsights.Extensibility;
using Azure.Monitor.OpenTelemetry.Exporter;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting PolicyEquityAndInvoicingMgt Endpoint.In");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console();

            var appInsightsConnectionString = context.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
            if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
            {
                configuration.WriteTo.ApplicationInsights(
                    services.GetRequiredService<TelemetryConfiguration>(),
                    TelemetryConverter.Traces);
            }
        })
        .NServiceBusEnvironmentConfiguration("RiskInsure.PolicyEquityAndInvoicingMgt.Endpoint",
        (config, endpoint, routing) =>
        {
          // Route commands to PolicyEquityAndInvoicingMgt Endpoint
          //  routing.RouteToEndpoint(typeof(RecordPayment), "RiskInsure.PolicyEquityAndInvoicingMgt.Endpoint");
        })
        .ConfigureServices((context, services) =>
        {
            var appInsightsConnectionString = context.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
            if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
            {
                services.AddApplicationInsightsTelemetryWorkerService();

                services.AddOpenTelemetry()
                    .WithTracing(tracing => tracing
                        .AddSource("NServiceBus.Core")
                        .AddAzureMonitorTraceExporter())
                    .WithMetrics(metrics => metrics
                        .AddMeter("NServiceBus.Core")
                        .AddAzureMonitorMetricExporter());
            }

            // Register Cosmos DB container for billing data (not sagas - sagas configured in NServiceBus persistence)
            var cosmosConnectionString = context.Configuration.GetConnectionString("CosmosDb")
                ?? throw new InvalidOperationException("CosmosDb connection string not configured");

            var databaseName = context.Configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
            var policyEquityAndInvoicingMgtContainerName = context.Configuration["CosmosDb:PolicyEquityAndInvoicingMgtContainerName"] ?? "PolicyEquityAndInvoicingMgt";

            var cosmosClient = new CosmosClient(cosmosConnectionString);
            var container = cosmosClient.GetContainer(databaseName, policyEquityAndInvoicingMgtContainerName);
            services.AddSingleton(container);

            // Register repositories
            services.AddSingleton<IPolicyEquityAndInvoicingAccountRepository, PolicyEquityAndInvoicingAccountRepository>();

            // Register managers
            services.AddScoped<IPolicyEquityAndInvoicingPaymentManager, PolicyEquityAndInvoicingPaymentManager>();
            services.AddScoped<IPolicyEquityAndInvoicingAccountManager, PolicyEquityAndInvoicingAccountManager>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "PolicyEquityAndInvoicingMgt Endpoint.In terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
