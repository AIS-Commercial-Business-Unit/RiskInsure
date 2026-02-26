using RiskInsure.Customer.Infrastructure;
using Serilog;

var builder = Host.CreateDefaultBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.UseSerilog();

// Application Insights telemetry (auto-reads APPLICATIONINSIGHTS_CONNECTION_STRING env var)
builder.ConfigureServices((context, services) =>
{
    services.AddApplicationInsightsTelemetryWorkerService();
});

// NServiceBus configuration
builder.NServiceBusEnvironmentConfiguration("RiskInsure.Customer.Endpoint");

var host = builder.Build();

await host.RunAsync();
