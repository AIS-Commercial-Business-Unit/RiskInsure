using Microsoft.Azure.Cosmos;
using RiskInsure.Customer.Domain.Managers;
using RiskInsure.Customer.Domain.Repositories;
using RiskInsure.Customer.Domain.Validation;
using RiskInsure.Customer.Infrastructure;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// NServiceBus configuration (send-only endpoint)
builder.Host.NServiceBusEnvironmentConfiguration("RiskInsure.Customer.Api");

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Cosmos DB with System.Text.Json serializer
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("CosmosDb") ??
                          throw new InvalidOperationException("CosmosDb connection string is required");
    
    // Configure CosmosClient to use System.Text.Json serialization
    var cosmosClientOptions = new CosmosClientOptions
    {
        ConnectionMode = ConnectionMode.Direct,
        Serializer = new CosmosSystemTextJsonSerializer(new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        }),
        RequestTimeout = TimeSpan.FromSeconds(60),  // Increased for emulator
        MaxRetryAttemptsOnRateLimitedRequests = 5,
        MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
    };
    
    return new CosmosClient(connectionString, cosmosClientOptions);
});

builder.Services.AddSingleton(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    // Get existing container (pre-created by init-cosmosdb.ps1)
    var databaseName = configuration["CosmosDb:DatabaseName"] ?? "RiskInsure";
    var containerName = configuration["CosmosDb:ContainerName"] ?? "customer";
    return cosmosClient.GetContainer(databaseName, containerName);
});

// Domain services
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerValidator, CustomerValidator>();
builder.Services.AddScoped<ICustomerManager, CustomerManager>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
