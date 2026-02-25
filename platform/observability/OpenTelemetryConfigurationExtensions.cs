using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace RiskInsure.Observability;

/// <summary>
/// Shared OpenTelemetry configuration for all RiskInsure services.
/// 
/// When deployed to Azure Container Apps with Application Insights linked,
/// the APPLICATIONINSIGHTS_CONNECTION_STRING environment variable is automatically
/// injected and telemetry (traces, metrics, logs) is exported to Application Insights.
/// 
/// Locally, no Application Insights exporter is registered — existing Serilog
/// console logging provides developer visibility.
/// 
/// NServiceBus 9.x automatically emits traces and metrics under the "NServiceBus.Core"
/// ActivitySource when OpenTelemetry is configured. See:
/// https://docs.particular.net/nservicebus/operations/opentelemetry
/// </summary>
public static class OpenTelemetryConfigurationExtensions
{
    /// <summary>
    /// Configures OpenTelemetry tracing, metrics, and logging for an NServiceBus
    /// Endpoint.In (worker) project. Only exports to Application Insights when
    /// the APPLICATIONINSIGHTS_CONNECTION_STRING environment variable is present.
    /// </summary>
    public static IServiceCollection AddRiskInsureOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        var connectionString = GetApplicationInsightsConnectionString(configuration);
        var hasAppInsights = !string.IsNullOrWhiteSpace(connectionString);

        if (!hasAppInsights)
        {
            Console.WriteLine($"[OpenTelemetry] No Application Insights connection string found for '{serviceName}'. " +
                              "Telemetry will not be exported. This is expected for local development.");
            return services;
        }

        Console.WriteLine($"[OpenTelemetry] Configuring Application Insights export for '{serviceName}'");

        var resourceBuilder = CreateResourceBuilder(configuration, serviceName);

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource("NServiceBus.Core")       // NServiceBus 9.x automatic instrumentation
                    .AddHttpClientInstrumentation()
                    .AddSource(serviceName)               // Custom ActivitySource for domain operations
                    .AddAzureMonitorTraceExporter(options =>
                    {
                        options.ConnectionString = connectionString;
                    });
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter("NServiceBus.Core")         // NServiceBus built-in metrics
                    .AddRuntimeInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(serviceName)                 // Custom metrics
                    .AddAzureMonitorMetricExporter(options =>
                    {
                        options.ConnectionString = connectionString;
                    });
            });

        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(otelLogging =>
            {
                otelLogging.SetResourceBuilder(resourceBuilder);
                otelLogging.IncludeFormattedMessage = true;
                otelLogging.IncludeScopes = true;
                otelLogging.AddAzureMonitorLogExporter(options =>
                {
                    options.ConnectionString = connectionString;
                });
            });
        });

        return services;
    }

    /// <summary>
    /// Configures OpenTelemetry tracing, metrics, and logging for an ASP.NET Core
    /// API project. Includes ASP.NET Core HTTP request/response instrumentation
    /// in addition to the base NServiceBus + runtime instrumentation.
    /// Only exports to Application Insights when the APPLICATIONINSIGHTS_CONNECTION_STRING
    /// environment variable is present.
    /// </summary>
    public static IServiceCollection AddRiskInsureOpenTelemetryForApi(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        var connectionString = GetApplicationInsightsConnectionString(configuration);
        var hasAppInsights = !string.IsNullOrWhiteSpace(connectionString);

        if (!hasAppInsights)
        {
            Console.WriteLine($"[OpenTelemetry] No Application Insights connection string found for '{serviceName}'. " +
                              "Telemetry will not be exported. This is expected for local development.");
            return services;
        }

        Console.WriteLine($"[OpenTelemetry] Configuring Application Insights export for '{serviceName}'");

        var resourceBuilder = CreateResourceBuilder(configuration, serviceName);

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource("NServiceBus.Core")
                    .AddAspNetCoreInstrumentation()       // HTTP request tracing
                    .AddHttpClientInstrumentation()
                    .AddSource(serviceName)
                    .AddAzureMonitorTraceExporter(options =>
                    {
                        options.ConnectionString = connectionString;
                    });
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter("NServiceBus.Core")
                    .AddAspNetCoreInstrumentation()       // HTTP request metrics
                    .AddRuntimeInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(serviceName)
                    .AddAzureMonitorMetricExporter(options =>
                    {
                        options.ConnectionString = connectionString;
                    });
            });

        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(otelLogging =>
            {
                otelLogging.SetResourceBuilder(resourceBuilder);
                otelLogging.IncludeFormattedMessage = true;
                otelLogging.IncludeScopes = true;
                otelLogging.AddAzureMonitorLogExporter(options =>
                {
                    options.ConnectionString = connectionString;
                });
            });
        });

        return services;
    }

    /// <summary>
    /// Resolves the Application Insights connection string from configuration.
    /// Azure Container Apps automatically injects APPLICATIONINSIGHTS_CONNECTION_STRING
    /// when Application Insights is linked, so no per-environment config is needed.
    /// </summary>
    private static string? GetApplicationInsightsConnectionString(IConfiguration configuration)
    {
        // Azure Container Apps injects this automatically when App Insights is linked
        return configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? configuration["ApplicationInsights:ConnectionString"];
    }

    private static ResourceBuilder CreateResourceBuilder(
        IConfiguration configuration,
        string serviceName)
    {
        return ResourceBuilder.CreateDefault()
            .AddService(serviceName)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = configuration["ASPNETCORE_ENVIRONMENT"]
                    ?? configuration["DOTNET_ENVIRONMENT"]
                    ?? "Production"
            });
    }
}
