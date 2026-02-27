using RiskInsure.Modernization.Reindex.Services;
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

    // Health check endpoint
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "reindex" }));

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
