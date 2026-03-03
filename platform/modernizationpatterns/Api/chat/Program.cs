using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.ApplicationInsights.Extensibility;
using RiskInsure.Modernization.Chat.Services;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Modernization Patterns Chat API");

    var builder = WebApplication.CreateBuilder(args);

    // Application Insights (when connection string is available in production)
    var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    if (!string.IsNullOrEmpty(appInsightsConnectionString))
    {
        builder.Services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = appInsightsConnectionString;
        });
    }

    // Serilog with Application Insights sink
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console();

        // Write to Application Insights if configured
        var telemetryConfig = services.GetService<TelemetryConfiguration>();
        if (telemetryConfig != null)
        {
            configuration.WriteTo.ApplicationInsights(telemetryConfig, TelemetryConverter.Traces);
        }
    });

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

    // Health check endpoints for Container Apps probes
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "chat-api" }));
    app.MapGet("/health/ready", (IServiceProvider sp) =>
    {
        // Check if critical services are available
        try
        {
            var openAi = sp.GetService<IOpenAiService>();
            var search = sp.GetService<ISearchService>();

            if (openAi == null || search == null)
            {
                return Results.Problem("Required services not available", statusCode: 503);
            }

            return Results.Ok(new { status = "ready", services = new[] { "openai", "search" } });
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
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
