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

// Initialize Cosmos DB container on startup
Log.Information("Initializing Cosmos DB database RiskInsure and container ratingunderwriting");

// Create logger for initialization
var loggerFactory = LoggerFactory.Create(logBuilder =>
{
    logBuilder.AddSerilog(Log.Logger);
});
var cosmosLogger = loggerFactory.CreateLogger<CosmosDbInitializer>();

var cosmosInitializer = new CosmosDbInitializer(cosmosClient, builder.Configuration, cosmosLogger);
var container = await cosmosInitializer.InitializeAsync();

// Register the initialized Container
builder.Services.AddSingleton(container);

// Domain services
builder.Services.AddScoped<IQuoteRepository, QuoteRepository>();
builder.Services.AddScoped<IQuoteManager, QuoteManager>();
builder.Services.AddScoped<IUnderwritingEngine, UnderwritingEngine>();
builder.Services.AddScoped<IRatingEngine, RatingEngine>();

// API
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ✅ Health Check service registration
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers();

app.MapHealthChecks("/health");

app.Run();
