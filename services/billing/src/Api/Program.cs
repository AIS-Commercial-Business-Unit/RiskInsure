using Microsoft.Azure.Cosmos;
using NServiceBus;
using RiskInsure.Billing.Domain.Contracts.Commands;
using RiskInsure.Billing.Domain.Managers;
using RiskInsure.Billing.Domain.Services.BillingDb;
using RiskInsure.Billing.Infrastructure;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Billing API");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog();

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
                Title = "RiskInsure Billing API",
                Version = "v1",
                Description = """
                    ## Overview
                    The Billing API provides RESTful endpoints for managing insurance policy billing accounts and payment operations.
                    
                    ## Capabilities
                    
                    ### Billing Accounts (`/api/billing/accounts`)
                    - **Create** new billing accounts for insurance policies
                    - **Activate** pending accounts to begin billing
                    - **Update** premium amounts and billing cycles
                    - **Suspend** accounts temporarily (non-payment, policy issues)
                    - **Close** accounts permanently
                    
                    ### Billing Payments (`/api/billing/payments`)
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
    var billingContainerName = builder.Configuration["CosmosDb:BillingContainerName"] ?? "Billing";

    // Configure CosmosClient to use System.Text.Json serialization and Direct mode for best performance
    var cosmosClientOptions = new CosmosClientOptions
    {
        ConnectionMode = ConnectionMode.Direct, // Direct mode is ~3x faster than Gateway mode
        Serializer = new CosmosSystemTextJsonSerializer(new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        }),
        RequestTimeout = TimeSpan.FromSeconds(10), // Set reasonable timeout
        MaxRetryAttemptsOnRateLimitedRequests = 3,
        MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(5)
    };
    
    var cosmosClient = new CosmosClient(cosmosConnectionString, cosmosClientOptions);

    // Initialize database and container on startup
    // For serverless accounts, pass throughput: null
    // For provisioned accounts, specify RU/s (e.g., throughput: 400)
    Log.Information("Initializing Cosmos DB database {DatabaseName} and container {ContainerName}", databaseName, billingContainerName);
    await CosmosDbInitializer.EnsureDbAndContainerAsync(
        cosmosClient, 
        databaseName, 
        billingContainerName, 
        "/accountId",
        databaseThroughput: 1000); // Database-level: 1000 RU/s shared across ALL containers (FREE TIER)
    Log.Information("Cosmos DB database initialization complete");

    var container = cosmosClient.GetContainer(databaseName, billingContainerName);
    builder.Services.AddSingleton(container);

    // Register repositories
    builder.Services.AddSingleton<IBillingAccountRepository, BillingAccountRepository>();

    // Register managers
    builder.Services.AddScoped<IBillingPaymentManager, BillingPaymentManager>();
    builder.Services.AddScoped<IBillingAccountManager, BillingAccountManager>();

    // Configure NServiceBus (send-only endpoint with routing)
    builder.Host.NServiceBusEnvironmentConfiguration(
        "RiskInsure.Billing.Api",
        (config, endpoint, routing) =>
        {
            endpoint.SendOnly(); // CRITICAL: API only sends/publishes, doesn't receive messages
            
            // Route commands to Billing Endpoint
            routing.RouteToEndpoint(typeof(RecordPayment), "RiskInsure.Billing.Endpoint");
        });

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
    Log.Fatal(ex, "Billing API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
