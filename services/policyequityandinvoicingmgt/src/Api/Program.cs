using Microsoft.Azure.Cosmos;
using NServiceBus;
using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Contracts.Commands;
using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Managers;
using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Services.PolicyEquityAndInvoicingDb;
using RiskInsure.PolicyEquityAndInvoicingMgt.Infrastructure;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using Microsoft.ApplicationInsights.Extensibility;
using Azure.Monitor.OpenTelemetry.Exporter;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting PolicyEquityAndInvoicingMgt API");

    var builder = WebApplication.CreateBuilder(args);
    var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
    var enableApplicationInsights = !string.IsNullOrWhiteSpace(appInsightsConnectionString);

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

    // Add controllers with JSON options for enum string conversion
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info = new()
            {
                Title = "RiskInsure PolicyEquityAndInvoicingMgt API",
                Version = "v1",
                Description = """
                    ## Overview
                    The PolicyEquityAndInvoicingMgt API provides RESTful endpoints for managing insurance policy billing accounts and payment operations.

                    ## Capabilities

                    ### Billing Accounts (`/api/policyequityandinvoicingmgt/accounts`)
                    - **Create** new billing accounts for insurance policies
                    - **Activate** pending accounts to begin billing
                    - **Update** premium amounts and billing cycles
                    - **Suspend** accounts temporarily (non-payment, policy issues)
                    - **Close** accounts permanently

                    ### Billing Payments (`/api/policyequityandinvoicingmgt/payments`)
                    - **Record payments** synchronously with immediate validation
                    - **Submit payments** asynchronously for background processing
                    - Track payment history and outstanding balances

                    ## Processing Patterns

                    - **Synchronous Endpoints**: Return immediate results (200 OK, 400 Bad Request, 404 Not Found)
                    - **Asynchronous Endpoints**: Accept commands for background processing (202 Accepted)
                    - **Event-Driven**: All operations publish domain events for downstream subscribers

                    ## Business Rules

                    - Accounts start in **Pending** status and must be activated
                    - Payments can only be recorded for **Active** accounts
                    - Premium amounts must be non-negative
                    - Policy numbers must be unique per customer
                    - All operations are idempotent (safe to retry)

                    ## Authentication
                    Currently operating in development mode without authentication. Production will require JWT bearer tokens.
                    """,
                Contact = new()
                {
                    Name = "RiskInsure Development Team",
                    Email = "dev@riskinsure.example.com"
                }
            };
            return Task.CompletedTask;
        });
    });


    // Configure Cosmos DB - Billing data container
    var cosmosConnectionString = builder.Configuration.GetConnectionString("CosmosDb")
        ?? throw new InvalidOperationException("CosmosDb connection string not configured");

    var databaseName = builder.Configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
    var policyEquityAndInvoicingMgtContainerName = builder.Configuration["CosmosDb:PolicyEquityAndInvoicingMgtContainerName"] ?? "policyequityandinvoicingmgt";

    // Configure CosmosClient to use System.Text.Json serialization and Gateway mode for startup resilience
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

    // Initialize database and container on startup
    // For serverless accounts, pass throughput: null
    // For provisioned accounts, specify RU/s (e.g., throughput: 400)
    Log.Information("Initializing Cosmos DB database {DatabaseName} and container {ContainerName}", databaseName, policyEquityAndInvoicingMgtContainerName);
    _ = Task.Run(async () =>
    {
        try
        {
            await CosmosDbInitializer.EnsureDbAndContainerAsync(
                cosmosClient,
                databaseName,
                policyEquityAndInvoicingMgtContainerName,
                "/accountId",
                databaseThroughput: 1000); // Database-level: 1000 RU/s shared across ALL containers (FREE TIER)

            Log.Information(
                "Cosmos bootstrap for container {ContainerName} completed in background.",
                policyEquityAndInvoicingMgtContainerName);
        }
        catch (CosmosException ex)
        {
            Log.Warning(
                ex,
                "Cosmos bootstrap for container {ContainerName} did not complete in background. API remains online and will retry through normal request paths.",
                policyEquityAndInvoicingMgtContainerName);
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                "Unexpected failure while bootstrapping Cosmos container {ContainerName} in background.",
                policyEquityAndInvoicingMgtContainerName);
        }
    });
    Log.Information("Cosmos DB database initialization complete");

    var container = cosmosClient.GetContainer(databaseName, policyEquityAndInvoicingMgtContainerName);
    builder.Services.AddSingleton(container);

    // Register repositories
    builder.Services.AddSingleton<IPolicyEquityAndInvoicingAccountRepository, PolicyEquityAndInvoicingAccountRepository>();

    // Register managers
    builder.Services.AddScoped<IPolicyEquityAndInvoicingPaymentManager, PolicyEquityAndInvoicingPaymentManager>();
    builder.Services.AddScoped<IPolicyEquityAndInvoicingAccountManager, PolicyEquityAndInvoicingAccountManager>();

    // Configure NServiceBus (send-only endpoint with routing)
    builder.Host.NServiceBusEnvironmentConfiguration(
        "RiskInsure.PolicyEquityAndInvoicingMgt.Api",
        (config, endpoint, routing) =>
        {
            endpoint.SendOnly(); // CRITICAL: API only sends/publishes, doesn't receive messages

            // Route commands to PolicyEquityAndInvoicingMgt Endpoint
            routing.RouteToEndpoint(typeof(RecordPayment), "RiskInsure.PolicyEquityAndInvoicingMgt.Endpoint");
        },
        isSendOnly: true);

    var app = builder.Build();

    // Configure middleware
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "PolicyEquityAndInvoicingMgt API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
