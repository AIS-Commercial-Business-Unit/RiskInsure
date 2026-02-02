using Microsoft.Azure.Cosmos;
using NServiceBus;
using RiskInsure.Billing.Domain.Contracts.Commands;
using RiskInsure.Billing.Domain.Services.BillingDb;
using Scalar.AspNetCore;
using Serilog;
using Infrastructure;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Billing API");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog();

    // Add controllers
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi();

    // Configure Cosmos DB - Billing data container
    var cosmosConnectionString = builder.Configuration.GetConnectionString("CosmosDb")
        ?? throw new InvalidOperationException("CosmosDb connection string not configured");

    var databaseName = builder.Configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
    var billingContainerName = builder.Configuration["CosmosDb:BillingContainerName"] ?? "Billing";

    var cosmosClient = new CosmosClient(cosmosConnectionString);
    var container = cosmosClient.GetContainer(databaseName, billingContainerName);
    builder.Services.AddSingleton(container);

    // Register repositories
    builder.Services.AddSingleton<IBillingAccountRepository, BillingAccountRepository>();

    // Configure NServiceBus (send-only endpoint with routing)
    builder.Host.NServiceBusEnvironmentConfiguration(
        "RiskInsure.Billing.Api",
        (config, endpoint, routing) =>
        {
            //endpoint.SendOnly();
            
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
