using RiskInsure.FileRetrieval.Infrastructure;
using NServiceBus;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add infrastructure services (Cosmos DB, repositories, Key Vault)
builder.Services.AddInfrastructure(builder.Configuration);

// Add controllers
builder.Services.AddControllers();

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

// Configure NServiceBus endpoint
var connectionString = builder.Configuration.GetConnectionString("ServiceBus") 
    ?? throw new InvalidOperationException("ServiceBus connection string is not configured");

var transport = new AzureServiceBusTransport(connectionString, TopicTopology.Default);
var endpointConfiguration = new EndpointConfiguration("FileRetrieval.API");

endpointConfiguration.UseTransport(transport);

// Configure message routing - send commands to Worker
var routing = endpointConfiguration.UseTransport(transport);
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

// This is a send-only endpoint (API doesn't handle messages, just sends commands)
endpointConfiguration.SendOnly();

// Use JSON serialization
endpointConfiguration.UseSerialization<SystemJsonSerializer>();

// Configure conventions for commands and events
var conventions = endpointConfiguration.Conventions();
conventions.DefiningCommandsAs(type =>
    type.Namespace?.StartsWith("FileRetrieval.Contracts.Commands") == true);
conventions.DefiningEventsAs(type =>
    type.Namespace?.StartsWith("FileRetrieval.Contracts.Events") == true);

builder.UseNServiceBus(endpointConfiguration);

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

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
