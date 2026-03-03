using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using Microsoft.ApplicationInsights.Extensibility;
using Azure.Monitor.OpenTelemetry.Exporter;
using RiskInsure.FundTransferMgt.Domain.Managers;
using RiskInsure.FundTransferMgt.Domain.Repositories;
using RiskInsure.FundTransferMgt.Domain.Services;
using RiskInsure.FundTransferMgt.Infrastructure;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.ApplicationInsights(
                services.GetRequiredService<TelemetryConfiguration>(),
                TelemetryConverter.Traces))
        .NServiceBusEnvironmentConfiguration("RiskInsure.FundTransferMgt.Endpoint",
        (config, endpoint, routing) =>
        {
          // Route commands to Billing Endpoint
          //  routing.RouteToEndpoint(typeof(RecordPayment), "RiskInsure.Billing.Endpoint");
        })
        .ConfigureServices((context, services) =>
        {
            // Application Insights telemetry (auto-reads APPLICATIONINSIGHTS_CONNECTION_STRING env var)
            services.AddApplicationInsightsTelemetryWorkerService();

            // OpenTelemetry: export NServiceBus traces and metrics to Azure Monitor
            services.AddOpenTelemetry()
                .WithTracing(tracing => tracing
                    .AddSource("NServiceBus.Core")
                    .AddAzureMonitorTraceExporter())
                .WithMetrics(metrics => metrics
                    .AddMeter("NServiceBus.Core")
                    .AddAzureMonitorMetricExporter());

            var cosmosConnectionString = context.Configuration.GetConnectionString("CosmosDb");
            if (string.IsNullOrEmpty(cosmosConnectionString))
            {
                throw new InvalidOperationException("CosmosDb connection string is required");
            }

            var cosmosClient = new CosmosClient(cosmosConnectionString);
            var database = cosmosClient.GetDatabase("RiskInsure");

            // Register repositories
            services.AddScoped<IPaymentMethodRepository>(sp =>
            {
                var container = database.GetContainer("FundTransferMgt-PaymentMethods");
                var logger = sp.GetRequiredService<ILogger<PaymentMethodRepository>>();
                return new PaymentMethodRepository(container, logger);
            });

            services.AddScoped<ITransactionRepository>(sp =>
            {
                var container = database.GetContainer("FundTransferMgt-Transactions");
                var logger = sp.GetRequiredService<ILogger<TransactionRepository>>();
                return new TransactionRepository(container, logger);
            });

            // Register payment gateway (mock)
            services.AddSingleton<IPaymentGateway>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<MockPaymentGateway>>();
                var config = new MockGatewayConfiguration { AlwaysSucceed = true };
                return new MockPaymentGateway(logger, config);
            });

            // Register managers
            services.AddScoped<IPaymentMethodManager, PaymentMethodManager>();
            services.AddScoped<IFundTransferManager, FundTransferManager>();
        })
        .Build();

    Log.Information("Starting FundTransferMgt Endpoint");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Endpoint terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
