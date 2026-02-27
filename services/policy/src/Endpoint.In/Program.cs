using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RiskInsure.Policy.Domain.Managers;
using RiskInsure.Policy.Domain.Repositories;
using RiskInsure.Policy.Domain.Services;
using Serilog;
using RiskInsure.Policy.Infrastructure;
using System.Text.Json;
using System.Text.Json.Serialization;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Policy Endpoint.In");

    var builder = Host.CreateDefaultBuilder(args);

    builder.UseSerilog();

    builder.ConfigureServices((context, services) =>
    {
        // Application Insights telemetry (auto-reads APPLICATIONINSIGHTS_CONNECTION_STRING env var)
        services.AddApplicationInsightsTelemetryWorkerService();

        // Configure Cosmos DB with custom serializer
        var cosmosConnectionString = context.Configuration.GetConnectionString("CosmosDb")
            ?? throw new InvalidOperationException("CosmosDb connection string not configured");

        var databaseName = context.Configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
        var containerName = context.Configuration["CosmosDb:ContainerName"] ?? "policy";

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

        // Register repositories and services
        services.AddSingleton<IPolicyRepository, PolicyRepository>();
        services.AddSingleton<IPolicyNumberGenerator, PolicyNumberGenerator>();
        services.AddSingleton<IPolicyManager, PolicyManager>();
    });

    // Configure NServiceBus
    builder.NServiceBusEnvironmentConfiguration("RiskInsure.Policy.Endpoint",
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
    Log.Fatal(ex, "Policy Endpoint.In terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
