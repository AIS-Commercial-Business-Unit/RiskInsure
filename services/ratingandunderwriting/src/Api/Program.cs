using Infrastructure;
using Microsoft.Azure.Cosmos;
using NServiceBus;
using RiskInsure.RatingAndUnderwriting.Domain.Managers;
using RiskInsure.RatingAndUnderwriting.Domain.Repositories;
using RiskInsure.RatingAndUnderwriting.Domain.Services;
using Serilog;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// NServiceBus (send-only endpoint for API)
builder.Host.NServiceBusEnvironmentConfiguration("RiskInsure.RatingAndUnderwriting.Api");

// Cosmos DB
var cosmosConnectionString = builder.Configuration.GetConnectionString("CosmosDb");
var cosmosClient = new CosmosClient(cosmosConnectionString, new CosmosClientOptions
{
    Serializer = new CosmosSystemTextJsonSerializer()
});

builder.Services.AddSingleton(cosmosClient);
builder.Services.AddSingleton<CosmosDbInitializer>();

// Register Container (initialized on startup)
builder.Services.AddSingleton(async sp =>
{
    var initializer = sp.GetRequiredService<CosmosDbInitializer>();
    return await initializer.InitializeAsync();
});

// Domain services
builder.Services.AddScoped<IQuoteRepository, QuoteRepository>();
builder.Services.AddScoped<IQuoteManager, QuoteManager>();
builder.Services.AddScoped<IUnderwritingEngine, UnderwritingEngine>();
builder.Services.AddScoped<IRatingEngine, RatingEngine>();

// API
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Initialize Cosmos DB
await app.Services.GetRequiredService<Task<Container>>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
