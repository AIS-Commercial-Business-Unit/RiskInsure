using RiskInsure.Customer.Infrastructure;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using Microsoft.ApplicationInsights.Extensibility;
using Azure.Monitor.OpenTelemetry.Exporter;

var builder = Host.CreateDefaultBuilder(args);

// Configure Serilog bootstrap logger
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

builder.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.ApplicationInsights(
        services.GetRequiredService<TelemetryConfiguration>(),
        TelemetryConverter.Traces));

// Application Insights telemetry (auto-reads APPLICATIONINSIGHTS_CONNECTION_STRING env var)
builder.ConfigureServices((context, services) =>
{
    services.AddApplicationInsightsTelemetryWorkerService();

    // OpenTelemetry: export NServiceBus traces and metrics to Azure Monitor
    services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddSource("NServiceBus.Core")
            .AddAzureMonitorTraceExporter())
        .WithMetrics(metrics => metrics
            .AddMeter("NServiceBus.Core")
            .AddAzureMonitorMetricExporter());
});

// NServiceBus configuration
builder.NServiceBusEnvironmentConfiguration("RiskInsure.Customer.Endpoint");

var host = builder.Build();

await host.RunAsync();
