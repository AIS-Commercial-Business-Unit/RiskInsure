using RiskInsure.RatingAndUnderwriting.Infrastructure;
using Microsoft.Azure.Cosmos;
using RiskInsure.RatingAndUnderwriting.Domain.Managers;
using RiskInsure.RatingAndUnderwriting.Domain.Repositories;
using RiskInsure.RatingAndUnderwriting.Domain.Services;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using Microsoft.ApplicationInsights.Extensibility;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Core.Diagnostics;
using System.Diagnostics.Tracing;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

AzureEventSourceListener? azureEventSourceListener = null;

try
{
    Log.Information("Starting Rating & Underwriting Endpoint.In");

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
        .NServiceBusEnvironmentConfiguration("RiskInsure.RatingAndUnderwriting.Endpoint",
        (config, endpoint, routing) =>
        {
          // Route commands to Billing Endpoint
          //  routing.RouteToEndpoint(typeof(RecordPayment), "RiskInsure.Billing.Endpoint");
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
                        .AddSource("RiskInsure.RatingAndUnderwriting.Publishing")
                        .AddAzureMonitorTraceExporter())
                    .WithMetrics(metrics => metrics
                        .AddMeter("NServiceBus.Core")
                        .AddAzureMonitorMetricExporter());
            }

            // Register Cosmos DB container
            var cosmosConnectionString = context.Configuration.GetConnectionString("CosmosDb")
                ?? throw new InvalidOperationException("CosmosDb connection string not configured");

            var databaseName = context.Configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
            var containerName = context.Configuration["CosmosDb:ContainerName"] ?? "ratingunderwriting";

            var cosmosClient = new CosmosClient(cosmosConnectionString, new CosmosClientOptions
            {
                Serializer = new CosmosSystemTextJsonSerializer()
            });

            services.AddSingleton(cosmosClient);
            services.AddSingleton<CosmosDbInitializer>();

            // Register Container
            services.AddSingleton(sp =>
            {
                var client = sp.GetRequiredService<CosmosClient>();
                var database = client.GetDatabase(databaseName);
                return database.GetContainer(containerName);
            });

            // Domain services
            services.AddScoped<IQuoteRepository, QuoteRepository>();
            services.AddScoped<IQuoteManager, QuoteManager>();
            services.AddScoped<IUnderwritingEngine, UnderwritingEngine>();
            services.AddScoped<IRatingEngine, RatingEngine>();
        })
        .Build();

    var configuration = host.Services.GetRequiredService<IConfiguration>();
    var enableAzureSdkEventSourceVerbose =
        configuration.GetValue<bool>("Telemetry:EnableAzureSdkEventSourceVerbose");

    if (enableAzureSdkEventSourceVerbose)
    {
        var azureSdkLogger = host.Services
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

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Rating & Underwriting Endpoint.In terminated unexpectedly");
    throw;
}
finally
{
    azureEventSourceListener?.Dispose();
    await Log.CloseAndFlushAsync();
}