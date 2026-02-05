using Microsoft.Azure.Cosmos;
using NServiceBus;
using RiskInsure.FundTransferMgt.Domain.Managers;
using RiskInsure.FundTransferMgt.Domain.Repositories;
using RiskInsure.FundTransferMgt.Domain.Services;
using RiskInsure.FundTransferMgt.Infrastructure.Repositories;
using RiskInsure.FundTransferMgt.Infrastructure;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Fund Transfer Management API");

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
                Title = "RiskInsure Fund Transfer Management API",
                Version = "v1",
                Description = """
                    ## Overview
                    The Fund Transfer Management API provides RESTful endpoints for managing payment methods, fund transfers, and refunds.
                    
                    ## Capabilities
                    
                    ### Payment Methods (`/api/payment-methods`)
                    - **Add** credit cards and ACH bank accounts
                    - **Retrieve** payment method details
                    - **List** payment methods by customer
                    - **Remove** payment methods (soft delete)
                    - **Tokenization** for PCI compliance
                    
                    ### Fund Transfers (`/api/transfers`)
                    - **Initiate** fund transfers from payment methods
                    - **Track** transfer status and history
                    - **Process** payments through payment gateway
                    
                    ### Refunds (`/api/refunds`)
                    - **Process** refunds for completed transfers
                    - **Track** refund status and amounts
                    - **Partial** and full refund support
                    
                    ## Processing Patterns
                    
                    - **Synchronous Endpoints**: Return immediate results (200 OK, 400 Bad Request, 404 Not Found)
                    - **Asynchronous Endpoints**: Accept commands for background processing (202 Accepted)
                    - **Event-Driven**: All operations publish domain events for downstream subscribers
                    
                    ## Business Rules
                    
                    - Payment methods must be validated before use
                    - Credit cards must pass Luhn checksum validation
                    - ACH routing numbers must pass ABA checksum validation
                    - Expired cards are rejected
                    - All amounts must be positive
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

    // Configure Cosmos DB
    var cosmosConnectionString = builder.Configuration.GetConnectionString("CosmosDb")
        ?? throw new InvalidOperationException("CosmosDb connection string not configured");

    var databaseName = builder.Configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
    var paymentMethodsContainerName = builder.Configuration["CosmosDb:PaymentMethodsContainerName"] ?? "FundTransferMgt-PaymentMethods";
    var transactionsContainerName = builder.Configuration["CosmosDb:TransactionsContainerName"] ?? "FundTransferMgt-Transactions";

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
    
    // Initialize database and containers on startup
    Log.Information("Initializing Cosmos DB database {DatabaseName}", databaseName);
    await CosmosDbInitializer.EnsureDbAndContainerAsync(
        cosmosClient, 
        databaseName, 
        paymentMethodsContainerName, 
        "/customerId",
        databaseThroughput: 1000); // Database-level: 1000 RU/s shared across ALL containers (FREE TIER)
    
    await CosmosDbInitializer.EnsureDbAndContainerAsync(
        cosmosClient, 
        databaseName, 
        transactionsContainerName, 
        "/customerId",
        databaseThroughput: 1000); // Reuses same database throughput
    
    var paymentMethodsContainer = cosmosClient.GetContainer(databaseName, paymentMethodsContainerName);
    var transactionsContainer = cosmosClient.GetContainer(databaseName, transactionsContainerName);

    // Register repositories
    builder.Services.AddSingleton(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<PaymentMethodRepository>>();
        return (IPaymentMethodRepository)new PaymentMethodRepository(paymentMethodsContainer, logger);
    });

    builder.Services.AddSingleton(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<TransactionRepository>>();
        return (ITransactionRepository)new TransactionRepository(transactionsContainer, logger);
    });

    // Register payment gateway (mock for development)
    builder.Services.AddSingleton<IPaymentGateway>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<MockPaymentGateway>>();
        var config = new MockGatewayConfiguration
        {
            AlwaysSucceed = true, // Development mode
            SimulatedDelayMs = 100
        };
        return new MockPaymentGateway(logger, config);
    });

    // Register managers
    builder.Services.AddScoped<IPaymentMethodManager, PaymentMethodManager>();
    builder.Services.AddScoped<IFundTransferManager, FundTransferManager>();

    // Configure NServiceBus (send-only endpoint with routing)
    builder.Host.NServiceBusEnvironmentConfiguration(
        "RiskInsure.FundTransferMgt.Api",
        (config, endpoint, routing) =>
        {
            endpoint.SendOnly(); // CRITICAL: API only sends/publishes, doesn't receive messages
            
            // Route commands to Fund Transfer Management Endpoint if needed
            // routing.RouteToEndpoint(typeof(SomeCommand), "RiskInsure.FundTransferMgt.Endpoint");
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
    Log.Fatal(ex, "Fund Transfer Management API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
