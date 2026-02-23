using FileRetrieval.Worker;
using RiskInsure.FileRetrieval.Infrastructure;
using RiskInsure.FileRetrieval.Infrastructure.Scheduling;
using NServiceBus;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = Host.CreateApplicationBuilder(args);

// T143: Add Application Insights for distributed tracing
builder.Services.AddApplicationInsightsTelemetryWorkerService(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = true;
});

// Add infrastructure services (Cosmos DB, repositories, Key Vault)
builder.Services.AddInfrastructure(builder.Configuration);

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Worker is running"))
    .AddCheck("cosmos-db", () =>
    {
        // Simple check - will be replaced with actual Cosmos DB health check in infrastructure layer
        return HealthCheckResult.Healthy("Cosmos DB configured");
    }, tags: new[] { "db", "cosmos" });

// Configure NServiceBus endpoint
var connectionString = builder.Configuration.GetConnectionString("ServiceBus") 
    ?? throw new InvalidOperationException("ServiceBus connection string is not configured");

var transport = new AzureServiceBusTransport(connectionString, TopicTopology.Default);
var endpointConfiguration = new EndpointConfiguration("FileRetrieval.Worker");

endpointConfiguration.UseTransport(transport);

// Configure message routing
var routing = endpointConfiguration.UseTransport(transport);
routing.RouteToEndpoint(
    typeof(FileRetrieval.Contracts.Commands.ProcessDiscoveredFile),
    "WorkflowOrchestrator"
);

// Enable installers for development
endpointConfiguration.EnableInstallers();

// Configure error queue
endpointConfiguration.SendFailedMessagesTo("error");

// Configure audit queue
endpointConfiguration.AuditProcessedMessagesTo("audit");

// Use JSON serialization
endpointConfiguration.UseSerialization<SystemJsonSerializer>();

// Configure conventions for commands and events
var conventions = endpointConfiguration.Conventions();
conventions.DefiningCommandsAs(type =>
    type.Namespace?.StartsWith("FileRetrieval.Contracts.Commands") == true);
conventions.DefiningEventsAs(type =>
    type.Namespace?.StartsWith("FileRetrieval.Contracts.Events") == true);

// Configure recoverability
var recoverability = endpointConfiguration.Recoverability();
recoverability.Immediate(settings => settings.NumberOfRetries(3));
recoverability.Delayed(settings => settings.NumberOfRetries(2));

builder.UseNServiceBus(endpointConfiguration);

// Register SchedulerHostedService for scheduled file checks
builder.Services.AddHostedService<SchedulerHostedService>();

// Add Worker hosted service (for message handling)
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
