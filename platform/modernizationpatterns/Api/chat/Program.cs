using Microsoft.AspNetCore.Authentication.JwtBearer;
using RiskInsure.Modernization.Chat.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Modernization Patterns Chat API");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog();

    // Controllers & OpenAPI
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // CORS for SWA integration
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowSWA", policyBuilder =>
        {
            policyBuilder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });

    // Register services
    builder.Services.AddScoped<IOpenAiService, OpenAiService>();
    builder.Services.AddScoped<ISearchService, SearchService>();
    builder.Services.AddScoped<IConversationService, ConversationService>();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors("AllowSWA");
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
