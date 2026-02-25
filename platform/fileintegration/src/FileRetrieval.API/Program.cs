using RiskInsure.FileRetrieval.Infrastructure;
using RiskInsure.FileRetrieval.Application.Services;
using FileRetrieval.Application.Protocols;
using NServiceBus;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider((context, options) =>
{
    var isDevelopment = context.HostingEnvironment.IsDevelopment();
    options.ValidateScopes = isDevelopment;
    options.ValidateOnBuild = isDevelopment;
});

// T143: Add Application Insights for distributed tracing
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = true;
    options.EnableQuickPulseMetricStream = true;
});

// Add infrastructure services (Cosmos DB, repositories, Key Vault)
builder.Services.AddInfrastructure(builder.Configuration);

// Defensive registration for message handler dependencies
// (keeps startup resilient if an older infrastructure assembly is loaded)
// builder.Services.AddScoped<ConfigurationService>();
// builder.Services.AddScoped<ExecutionHistoryService>();
// builder.Services.AddScoped<FileCheckService>();
// builder.Services.AddScoped<ProtocolAdapterFactory>();
// builder.Services.AddSingleton<TokenReplacementService>();
// builder.Services.AddSingleton<FileRetrievalMetricsService>();

// Add controllers
builder.Services.AddControllers();

// Add health checks for Azure Container Apps readiness/liveness probes
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API is running"))
    .AddCheck("cosmos-db", () => 
    {
        // Simple check - will be replaced with actual Cosmos DB health check in infrastructure layer
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Cosmos DB configured");
    }, tags: new[] { "db", "cosmos" });

// Add rate limiting to prevent abuse (T139)
builder.Services.AddRateLimiter(options =>
{
    // Fixed window rate limiter for API endpoints
    options.AddFixedWindowLimiter("api", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100; // 100 requests
        limiterOptions.Window = TimeSpan.FromMinutes(1); // per minute
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10; // Queue up to 10 requests
    });

    // Stricter rate limit for mutation operations (POST, PUT, DELETE)
    options.AddFixedWindowLimiter("api-write", limiterOptions =>
    {
        limiterOptions.PermitLimit = 20; // 20 requests
        limiterOptions.Window = TimeSpan.FromMinutes(1); // per minute
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 5;
    });

    // Global rejection behavior
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Rate limit exceeded",
            retryAfter = context.Lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfter)
                ? retryAfter.ToString()
                : "60 seconds"
        }, token);
    };
});

// Add OpenAPI/Swagger with JWT authentication support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "File Retrieval Configuration API",
        Version = "v1",
        Description = "API for managing client file retrieval configurations. " +
                      "This service enables automated checks of client file locations (FTP, HTTPS, Azure Blob Storage) " +
                      "on scheduled intervals and triggers workflow orchestration when files are discovered.",
        Contact = new OpenApiContact
        {
            Name = "RiskInsure Platform Team",
            Email = "platform@riskinsure.com"
        }
    });

    // Add JWT authentication to Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. " +
                      "Enter 'Bearer' [space] and then your token in the text input below. " +
                      "Example: 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Configure NServiceBus endpoint with dual transport support
builder.Host.NServiceBusEnvironmentConfiguration(
    "FileRetrieval.API",
    (config, endpoint, routing) =>
    {
        // This is a send-only endpoint (API doesn't handle messages, just sends commands)
        endpoint.SendOnly();

        // Configure message routing - send commands to Worker
        routing.RouteToEndpoint(
            typeof(FileRetrieval.Contracts.Commands.CreateConfiguration),
            "FileRetrieval.Worker"
        );
        routing.RouteToEndpoint(
            typeof(FileRetrieval.Contracts.Commands.UpdateConfiguration),
            "FileRetrieval.Worker"
        );
        routing.RouteToEndpoint(
            typeof(FileRetrieval.Contracts.Commands.DeleteConfiguration),
            "FileRetrieval.Worker"
        );
        routing.RouteToEndpoint(
            typeof(FileRetrieval.Contracts.Commands.ExecuteFileCheck),
            "FileRetrieval.Worker"
        );
    });

// Configure JWT authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");
var issuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JWT Issuer is not configured");
var audience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JWT Audience is not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.FromMinutes(5) // Allow 5 minutes clock skew
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT authentication failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var clientId = context.Principal?.FindFirst("clientId")?.Value;
                var userId = context.Principal?.FindFirst("sub")?.Value;
                logger.LogDebug("JWT token validated for user {UserId}, client {ClientId}", userId, clientId);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ClientAccess", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("clientId"));
});

var app = builder.Build();

// T140: Add comprehensive error handling middleware
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;

        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Unhandled exception occurred: {Message}", exception?.Message);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred while processing your request",
            Detail = app.Environment.IsDevelopment() ? exception?.Message : "Please contact support if the problem persists",
            Instance = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(problemDetails);
    });
});

// T141: Add security headers (CORS, CSP, HSTS)
app.Use(async (context, next) =>
{
    // HSTS (HTTP Strict Transport Security)
    context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    
    // Content Security Policy
    context.Response.Headers.Append("Content-Security-Policy", 
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:;");
    
    // X-Frame-Options (prevent clickjacking)
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    
    // X-Content-Type-Options (prevent MIME sniffing)
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    
    // Referrer-Policy
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    
    // Permissions-Policy (formerly Feature-Policy)
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");

    await next();
});

// CORS configuration
app.UseCors(policy =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
        ?? new[] { "https://localhost:5001", "https://app.riskinsure.com" };
    
    policy.WithOrigins(allowedOrigins)
          .AllowAnyMethod()
          .AllowAnyHeader()
          .AllowCredentials();
});

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Rate limiting middleware (must be before authentication)
app.UseRateLimiter();

// Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// Health check endpoints for Azure Container Apps
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                exception = e.Value.Exception?.Message
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result);
    }
});

// Simple liveness probe (just checks if API is responsive)
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Name == "self"
});

// Readiness probe (checks dependencies like Cosmos DB)
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db")
});

app.MapControllers();

app.Run();
