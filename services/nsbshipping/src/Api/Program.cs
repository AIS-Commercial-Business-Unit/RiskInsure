using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using NServiceBus.Transport.AzureServiceBus;
using RiskInsure.NsbShipping.Domain.Managers;
using RiskInsure.NsbShipping.Domain.Repositories;
using RiskInsure.NsbShipping.Infrastructure;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting NsbShipping API");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info = new()
            {
                Title = "RiskInsure NsbShipping API",
                Version = "v1",
                Description = "NsbShipping domain API"
            };
            return Task.CompletedTask;
        });
    });

    builder.Services.AddSingleton<Container>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILogger<CosmosDbInitializer>>();
        var initializer = new CosmosDbInitializer(config, logger);
        return initializer.InitializeAsync().GetAwaiter().GetResult();
    });

    builder.Services.AddScoped<IInventoryReservationRepository, InventoryReservationRepository>();
    builder.Services.AddScoped<IShipmentRepository, ShipmentRepository>();
    builder.Services.AddScoped<IInventoryManager, InventoryManager>();
    builder.Services.AddScoped<IShippingManager, ShippingManager>();

    builder.Host.UseNServiceBus(context =>
    {
        var endpointConfiguration = new EndpointConfiguration("RiskInsure.NsbShipping.Api");
        endpointConfiguration.SendOnly();
        var environment = context.HostingEnvironment.EnvironmentName;
        if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
        {
            var fqn = context.Configuration["AzureServiceBus:FullyQualifiedNamespace"];
            var transport = new AzureServiceBusTransport(fqn, new Azure.Identity.DefaultAzureCredential(), TopicTopology.Default);
            endpointConfiguration.UseTransport(transport);
        }
        else
        {
            var connectionString = context.Configuration.GetConnectionString("ServiceBus");
            var transport = new AzureServiceBusTransport(connectionString, TopicTopology.Default);
            endpointConfiguration.UseTransport(transport);
        }
        return endpointConfiguration;
    });

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "NsbShipping API terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
