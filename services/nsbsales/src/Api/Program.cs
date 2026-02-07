using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using NServiceBus.Transport.AzureServiceBus;
using RiskInsure.NsbSales.Domain.Managers;
using RiskInsure.NsbSales.Domain.Repositories;
using RiskInsure.NsbSales.Infrastructure;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting NsbSales API");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog();

    // Add controllers
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info = new()
            {
                Title = "RiskInsure Sales API",
                Version = "v1",
                Description = "API for placing orders in the Sales system"
            };
            return Task.CompletedTask;
        });
    });

    // Register Cosmos DB container (initialize after DI container is built)
    builder.Services.AddSingleton<Container>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILogger<CosmosDbInitializer>>();
        var initializer = new CosmosDbInitializer(config, logger);
        return initializer.InitializeAsync().GetAwaiter().GetResult();
    });

    // Register domain services
    builder.Services.AddScoped<IOrderRepository, OrderRepository>();
    builder.Services.AddScoped<IOrderManager, OrderManager>();

    // Configure NServiceBus for send-only (no handlers)
    builder.Host.UseNServiceBus(context =>
    {
        var endpointConfiguration = new EndpointConfiguration("RiskInsure.NsbSales.Api");
        
        // Send-only endpoint
        endpointConfiguration.SendOnly();
        
        // Configure transport
        var environment = context.HostingEnvironment.EnvironmentName;
        if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
        {
            var fqn = context.Configuration["AzureServiceBus:FullyQualifiedNamespace"];
            if (string.IsNullOrWhiteSpace(fqn))
            {
                throw new InvalidOperationException("AzureServiceBus:FullyQualifiedNamespace required");
            }
            var transport = new AzureServiceBusTransport(
                fqn,
                new Azure.Identity.DefaultAzureCredential(),
                TopicTopology.Default);
            endpointConfiguration.UseTransport(transport);
        }
        else
        {
            var connectionString = context.Configuration.GetConnectionString("ServiceBus");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ServiceBus connection string required");
            }
            var transport = new AzureServiceBusTransport(
                connectionString,
                TopicTopology.Default);
            endpointConfiguration.UseTransport(transport);
        }
        
        return endpointConfiguration;
    });

    // Configure CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    var app = builder.Build();

    // Configure middleware
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "NsbSales API terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
