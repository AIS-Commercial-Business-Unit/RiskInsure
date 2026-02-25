using RiskInsure.Customer.Infrastructure;
using RiskInsure.Observability;
using Serilog;

var builder = Host.CreateDefaultBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.UseSerilog();

// OpenTelemetry → Application Insights (only active when APPLICATIONINSIGHTS_CONNECTION_STRING is set)
builder.ConfigureServices((context, services) =>
{
    services.AddRiskInsureOpenTelemetry(context.Configuration, "RiskInsure.Customer.Endpoint");
});

// NServiceBus configuration
builder.NServiceBusEnvironmentConfiguration("RiskInsure.Customer.Endpoint");

var host = builder.Build();

await host.RunAsync();
