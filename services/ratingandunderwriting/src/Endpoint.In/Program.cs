using RiskInsure.RatingAndUnderwriting.Infrastructure;
using Microsoft.Azure.Cosmos;
using RiskInsure.RatingAndUnderwriting.Domain.Managers;
using RiskInsure.RatingAndUnderwriting.Domain.Repositories;
using RiskInsure.RatingAndUnderwriting.Domain.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Rating & Underwriting Endpoint.In");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .NServiceBusEnvironmentConfiguration("RiskInsure.RatingAndUnderwriting.Endpoint")
        .ConfigureServices((context, services) =>
        {
            // Register Cosmos DB container
            var cosmosConnectionString = context.Configuration.GetConnectionString("CosmosDb")
                ?? throw new InvalidOperationException("CosmosDb connection string not configured");

            var databaseName = context.Configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
            var containerName = context.Configuration["CosmosDb:ContainerName"] ?? "ratingunderwriting";

            var cosmosClient = new CosmosClient(cosmosConnectionString, new CosmosClientOptions
            {
                Serializer = new CosmosSystemTextJsonSerializer()
            });

            services.AddSingleton(cosmosClient);
            services.AddSingleton<CosmosDbInitializer>();

            // Register Container
            services.AddSingleton(sp =>
            {
                var client = sp.GetRequiredService<CosmosClient>();
                var database = client.GetDatabase(databaseName);
                return database.GetContainer(containerName);
            });

            // Domain services
            services.AddScoped<IQuoteRepository, QuoteRepository>();
            services.AddScoped<IQuoteManager, QuoteManager>();
            services.AddScoped<IUnderwritingEngine, UnderwritingEngine>();
            services.AddScoped<IRatingEngine, RatingEngine>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Rating & Underwriting Endpoint.In terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}