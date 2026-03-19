using FileProcessing.Worker;
using RiskInsure.FileProcessing.Infrastructure;
using RiskInsure.FileProcessing.Infrastructure.Scheduling;
using RiskInsure.FileProcessing.Application.Services;
using FileProcessing.Application.Protocols;
using NServiceBus;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .UseDefaultServiceProvider((context, options) =>
    {
        var isDevelopment = context.HostingEnvironment.IsDevelopment();
        options.ValidateScopes = isDevelopment;
        options.ValidateOnBuild = isDevelopment;
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        });
    })
    .ConfigureServices((context, services) =>
    {
        // T143: Add Application Insights for distributed tracing
        services.AddApplicationInsightsTelemetryWorkerService(options =>
        {
            options.ConnectionString = context.Configuration["ApplicationInsights:ConnectionString"];
            options.EnableAdaptiveSampling = true;
        });

        // Add infrastructure services (Cosmos DB, repositories, Key Vault)
        services.AddInfrastructure(context.Configuration);

        // Add health checks
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Worker is running"))
            .AddCheck("cosmos-db", () =>
            {
                // Simple check - will be replaced with actual Cosmos DB health check in infrastructure layer
                return HealthCheckResult.Healthy("Cosmos DB configured");
            }, tags: new[] { "db", "cosmos" });

        // Register SchedulerHostedService for scheduled file checks
        services.AddHostedService<SchedulerHostedService>();

        // Add Worker hosted service (for message handling)
        services.AddHostedService<Worker>();
    })
    .NServiceBusEnvironmentConfiguration(
        "FileProcessing.Worker",
        (config, endpoint, routing) =>
        {
            // Configure message routing
            routing.RouteToEndpoint(
                typeof(FileProcessing.Contracts.Commands.ParseDiscoveredFile),
                "FileProcessing.Worker"
            );

            routing.RouteToEndpoint(
                typeof(FileProcessing.Contracts.Commands.ProcessNachaRow),
                "FileProcessing.Worker"
            );
        })
    .Build();

await host.RunAsync();



