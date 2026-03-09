using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RiskInsure.PolicyLifeCycleMgt.Domain.Managers;
using RiskInsure.PolicyLifeCycleMgt.Domain.Repositories;
using RiskInsure.PolicyLifeCycleMgt.Domain.Services;
using RiskInsure.PolicyLifeCycleMgt.Endpoint.In.Configuration;
using Serilog;
using RiskInsure.PolicyLifeCycleMgt.Infrastructure;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using Microsoft.ApplicationInsights.Extensibility;
using Azure.Monitor.OpenTelemetry.Exporter;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting PolicyLifeCycleMgt Endpoint.In");

    var builder = Host.CreateDefaultBuilder(args);

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

        // Configure Cosmos DB with custom serializer
        var cosmosConnectionString = context.Configuration.GetConnectionString("CosmosDb")
            ?? throw new InvalidOperationException("CosmosDb connection string not configured");

        var databaseName = context.Configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
        var containerName = context.Configuration["CosmosDb:ContainerName"] ?? "policylifecycle";

        var cosmosClientOptions = new CosmosClientOptions
        {
            Serializer = new CosmosSystemTextJsonSerializer(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            }),
            ConnectionMode = ConnectionMode.Direct,
            RequestTimeout = TimeSpan.FromSeconds(10),
            MaxRetryAttemptsOnRateLimitedRequests = 3,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(5)
        };

        var cosmosClient = new CosmosClient(cosmosConnectionString, cosmosClientOptions);
        var container = cosmosClient.GetContainer(databaseName, containerName);
        services.AddSingleton(container);

        services.Configure<LifeCycleTrafficOptions>(
            context.Configuration.GetSection(LifeCycleTrafficOptions.SectionName));

        // Register repositories and services
        services.AddSingleton<ILifeCycleRepository, LifeCycleRepository>();
        services.AddSingleton<ILifeCycleNumberGenerator, LifeCycleNumberGenerator>();
        services.AddSingleton<ILifeCycleManager, LifeCycleManager>();
    });

    // Configure NServiceBus
    builder.NServiceBusEnvironmentConfiguration("RiskInsure.PolicyLifeCycleMgt.Endpoint",
        (config, endpoint, routing) =>
        {
          // Route commands to Billing Endpoint
          //  routing.RouteToEndpoint(typeof(RecordPayment), "RiskInsure.Billing.Endpoint");
        });

    var host = builder.Build();

    await host.RunAsync();

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "PolicyLifeCycleMgt Endpoint.In terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
