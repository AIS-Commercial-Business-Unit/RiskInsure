using Microsoft.Azure.Cosmos;
using RiskInsure.CustomerRelationshipsMgt.Domain.Managers;
using RiskInsure.CustomerRelationshipsMgt.Domain.Repositories;
using RiskInsure.CustomerRelationshipsMgt.Domain.Validation;
using RiskInsure.CustomerRelationshipsMgt.Infrastructure;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using Microsoft.ApplicationInsights.Extensibility;
using Azure.Monitor.OpenTelemetry.Exporter;

var builder = WebApplication.CreateBuilder(args);
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
var enableApplicationInsights = !string.IsNullOrWhiteSpace(appInsightsConnectionString);

// Configure Serilog with Application Insights sink
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();

    if (enableApplicationInsights)
    {
        configuration.WriteTo.ApplicationInsights(
            services.GetRequiredService<TelemetryConfiguration>(),
            TelemetryConverter.Traces);
    }
});

if (enableApplicationInsights)
{
    builder.Services.AddApplicationInsightsTelemetry();

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddSource("NServiceBus.Core")
            .AddAzureMonitorTraceExporter())
        .WithMetrics(metrics => metrics
            .AddMeter("NServiceBus.Core")
            .AddAzureMonitorMetricExporter());
}

// NServiceBus configuration (send-only endpoint)
builder.Host.NServiceBusEnvironmentConfiguration("RiskInsure.CustomerRelationshipsMgt.Api",
    (config, endpoint, routing) =>
    {
        endpoint.SendOnly();
    },
    isSendOnly: true);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
  // ✅ Add Health Checks service
builder.Services.AddHealthChecks();

// Cosmos DB with System.Text.Json serializer
var cosmosConnectionString = builder.Configuration.GetConnectionString("CosmosDb")
    ?? throw new InvalidOperationException("CosmosDb connection string is required");

var databaseName = builder.Configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
var containerName = builder.Configuration["CosmosDb:ContainerName"] ?? "customerrelationships";

var cosmosClientOptions = new CosmosClientOptions
{
    ConnectionMode = ConnectionMode.Gateway,
    Serializer = new CosmosSystemTextJsonSerializer(new System.Text.Json.JsonSerializerOptions
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    }),
    RequestTimeout = TimeSpan.FromSeconds(30),
    MaxRetryAttemptsOnRateLimitedRequests = 9,
    MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
};

var cosmosClient = new CosmosClient(cosmosConnectionString, cosmosClientOptions);

Log.Information("Initializing Cosmos DB database {DatabaseName} and container {ContainerName}",
    databaseName,
    containerName);

_ = Task.Run(async () =>
{
    try
    {
        await CosmosDbInitializer.EnsureDbAndContainerAsync(
            cosmosClient,
            databaseName,
            containerName,
            "/customerId");

        Log.Information(
            "Cosmos bootstrap for container {ContainerName} completed in background.",
            containerName);
    }
    catch (CosmosException ex)
    {
        Log.Warning(
            ex,
            "Cosmos bootstrap for container {ContainerName} did not complete in background. API remains online and will retry through normal request paths.",
            containerName);
    }
    catch (Exception ex)
    {
        Log.Error(
            ex,
            "Unexpected failure while bootstrapping Cosmos container {ContainerName} in background.",
            containerName);
    }
});

var container = cosmosClient.GetContainer(databaseName, containerName);
builder.Services.AddSingleton(container);

// Domain services
builder.Services.AddScoped<IRelationshipRepository, RelationshipRepository>();
builder.Services.AddScoped<IRelationshipValidator, RelationshipValidator>();
builder.Services.AddScoped<IRelationshipManager, RelationshipManager>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
