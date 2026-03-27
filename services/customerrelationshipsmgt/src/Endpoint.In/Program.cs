using Microsoft.Azure.Cosmos;
using RiskInsure.CustomerRelationshipsMgt.Infrastructure;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using Microsoft.ApplicationInsights.Extensibility;
using Azure.Monitor.OpenTelemetry.Exporter;

var builder = Host.CreateDefaultBuilder(args);

// Configure Serilog bootstrap logger
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

builder.UseSerilog((context, services, configuration) =>
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
});

// Application Insights telemetry (auto-reads APPLICATIONINSIGHTS_CONNECTION_STRING env var)
builder.ConfigureServices((context, services) =>
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

    var cosmosConnectionString = context.Configuration.GetConnectionString("CosmosDb")
                ?? throw new InvalidOperationException("CosmosDb connection string not configured");

    var databaseName = context.Configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
    var customerRelationshipsMgtContainerName = context.Configuration["CosmosDb:CustomerRelationshipsMgtContainerName"] ?? "customerrelationshipsmgt";

    var cosmosClient = new CosmosClient(cosmosConnectionString);
    var container = cosmosClient.GetContainer(databaseName, customerRelationshipsMgtContainerName);
    services.AddSingleton(container);
});

// NServiceBus configuration
builder.NServiceBusEnvironmentConfiguration("RiskInsure.CustomerRelationshipsMgt.Endpoint");

var host = builder.Build();

await host.RunAsync();
