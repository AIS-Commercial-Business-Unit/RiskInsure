using RiskInsure.ModernizationPatternsMgt.Domain.Services;
using RiskInsure.ModernizationPatternsMgt.Infrastructure.AzureOpenAi;
using RiskInsure.ModernizationPatternsMgt.Infrastructure.AzureSearch;
using RiskInsure.ModernizationPatternsMgt.Infrastructure.Chunking;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Modernization Patterns Reindex Service");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // Register services for dependency injection
    builder.Services.AddSingleton<IChunkingService, ChunkingService>();
    builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
    builder.Services.AddSingleton<IIndexingService, IndexingService>();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseSerilogRequestLogging();
    app.MapControllers();

    // Health check endpoints for Container Apps probes
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "reindex" }));
    app.MapGet("/health/ready", (IServiceProvider sp) =>
    {
        // Check if critical services are available
        try
        {
            var embedding = sp.GetService<IEmbeddingService>();
            var indexing = sp.GetService<IIndexingService>();

            if (embedding == null || indexing == null)
            {
                return Results.Problem("Required services not available", statusCode: 503);
            }

            return Results.Ok(new { status = "ready", services = new[] { "embedding", "indexing" } });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Readiness check failed");
            return Results.Problem("Service not ready", statusCode: 503);
        }
    });

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Reindex service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
