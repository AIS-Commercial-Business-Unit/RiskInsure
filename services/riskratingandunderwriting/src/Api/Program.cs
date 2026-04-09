using RiskInsure.RiskRatingAndUnderwriting.Infrastructure;
using Microsoft.Azure.Cosmos;
using NServiceBus;
using RiskInsure.RiskRatingAndUnderwriting.Domain.Managers;
using RiskInsure.RiskRatingAndUnderwriting.Domain.Repositories;
using RiskInsure.RiskRatingAndUnderwriting.Domain.Services;
using Serilog;
using Scalar.AspNetCore;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using Microsoft.ApplicationInsights.Extensibility;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Core.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
var enableApplicationInsights = !string.IsNullOrWhiteSpace(appInsightsConnectionString);
var enableAzureSdkEventSourceVerbose =
    builder.Configuration.GetValue<bool>("Telemetry:EnableAzureSdkEventSourceVerbose");
AzureEventSourceListener? azureEventSourceListener = null;

// Serilog bootstrap logger (replaced by full config once host is built)
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
            .AddSource("RiskInsure.RiskRatingAndUnderwriting.Publishing")
            .AddAzureMonitorTraceExporter())
        .WithMetrics(metrics => metrics
            .AddMeter("NServiceBus.Core")
            .AddAzureMonitorMetricExporter());
}

// NServiceBus (send-only endpoint for API)
builder.Host.NServiceBusEnvironmentConfiguration("RiskInsure.RiskRatingAndUnderwriting.Api",
    (config, endpoint, routing) =>
    {
        endpoint.SendOnly();
    },
    isSendOnly: true);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
// ✅ Health Check service registration
builder.Services.AddHealthChecks();

// Configure Cosmos DB with custom serializer
var cosmosConnectionString = builder.Configuration.GetConnectionString("CosmosDb")
    ?? throw new InvalidOperationException("CosmosDb connection string not configured");

var databaseName = builder.Configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
var containerName = builder.Configuration["CosmosDb:ContainerName"] ?? "riskratingandunderwriting";

var cosmosClientOptions = new CosmosClientOptions
{
    Serializer = new CosmosSystemTextJsonSerializer(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    }),
    ConnectionMode = ConnectionMode.Gateway,
    RequestTimeout = TimeSpan.FromSeconds(30),
    MaxRetryAttemptsOnRateLimitedRequests = 9,
    MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
};

var cosmosClient = new CosmosClient(cosmosConnectionString, cosmosClientOptions);

// Initialize database and container
Log.Information("Initializing Cosmos DB database {DatabaseName} and container {ContainerName}",
    databaseName, containerName);
_ = Task.Run(async () =>
{
    try
    {
        await CosmosDbInitializer.EnsureDbAndContainerAsync(
            cosmosClient,
            databaseName,
            containerName,
            "/quoteId");

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
builder.Services.AddScoped<IRiskQuoteRepository, RiskQuoteRepository>();
builder.Services.AddScoped<IRiskQuoteManager, RiskQuoteManager>();
builder.Services.AddScoped<IRiskUnderwritingEngine, RiskUnderwritingEngine>();
builder.Services.AddScoped<IRiskRatingEngine, RiskRatingEngine>();

var app = builder.Build();

if (enableAzureSdkEventSourceVerbose)
{
    var azureSdkLogger = app.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("AzureSdkEventSource");

    azureEventSourceListener = new AzureEventSourceListener(
        (eventArgs, message) =>
        {
            var source = eventArgs.EventSource?.Name ?? "AzureSDK";

            if (eventArgs.Level >= EventLevel.Warning)
            {
                azureSdkLogger.LogWarning(
                    "[AzureSDK:{Source}] {Message}",
                    source,
                    message);
            }
            else
            {
                azureSdkLogger.LogInformation(
                    "[AzureSDK:{Source}] {Message}",
                    source,
                    message);
            }
        },
        EventLevel.Verbose);

    azureSdkLogger.LogWarning("Azure SDK verbose EventSource logging is enabled.");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers();

app.MapHealthChecks("/health");

app.Run();
azureEventSourceListener?.Dispose();
