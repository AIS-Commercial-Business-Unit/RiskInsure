using Microsoft.Azure.Cosmos;
using RiskInsure.Policy.Domain.Managers;
using RiskInsure.Policy.Domain.Repositories;
using RiskInsure.Policy.Domain.Services;
using RiskInsure.Observability;
using Scalar.AspNetCore;
using Serilog;
using RiskInsure.Policy.Infrastructure;
using System.Text.Json;
using System.Text.Json.Serialization;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Policy API");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // OpenTelemetry → Application Insights (only active when APPLICATIONINSIGHTS_CONNECTION_STRING is set)
    builder.Services.AddRiskInsureOpenTelemetryForApi(builder.Configuration, "RiskInsure.Policy.Api");

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi();
 // ✅ Add Health Checks service
    builder.Services.AddHealthChecks();
    // Configure Cosmos DB with custom serializer
    var cosmosConnectionString = builder.Configuration.GetConnectionString("CosmosDb")
        ?? throw new InvalidOperationException("CosmosDb connection string not configured");

    var databaseName = builder.Configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
    var containerName = builder.Configuration["CosmosDb:ContainerName"] ?? "policy";

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

    // Initialize database and container
    Log.Information("Initializing Cosmos DB database {DatabaseName} and container {ContainerName}",
        databaseName, containerName);
    await CosmosDbInitializer.EnsureDbAndContainerAsync(
        cosmosClient,
        databaseName,
        containerName,
        "/policyId");

    var container = cosmosClient.GetContainer(databaseName, containerName);
    builder.Services.AddSingleton(container);

    // Register repositories and services
    builder.Services.AddSingleton<IPolicyRepository, PolicyRepository>();
    builder.Services.AddSingleton<IPolicyNumberGenerator, PolicyNumberGenerator>();
    builder.Services.AddSingleton<IPolicyManager, PolicyManager>();

    // Configure NServiceBus (send-only for API)
    builder.Host.NServiceBusEnvironmentConfiguration(
        "RiskInsure.Policy.Api",
        (config, endpoint, routing) =>
        {
            endpoint.SendOnly();
        },
        isSendOnly: true);

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
    await app.RunAsync();

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Policy API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
