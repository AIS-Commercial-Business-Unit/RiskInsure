# API Project Initialization

## Overview

This guide provides step-by-step instructions for creating an ASP.NET Core Web API project with NServiceBus integration, Cosmos DB persistence, and OpenAPI documentation using Scalar.

**Prerequisites**:
- .NET 10.0 SDK
- Azure Service Bus namespace (or connection string)
- Azure Cosmos DB account (or local emulator)
- Infrastructure project with NServiceBusConfigurationExtensions

---

## 1. Create API Project

```powershell
# Navigate to service directory
cd services/<ServiceName>/src

# Create ASP.NET Core Web API project
dotnet new webapi -n Api -o Api

# Add to solution
dotnet sln add Api/Api.csproj
```

---

## 2. Update Project File

Edit `Api/Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <RootNamespace>YourCompany.ServiceName.Api</RootNamespace>
  </PropertyGroup>
  
  <ItemGroup>
    <!-- ASP.NET Core packages (versions in Directory.Packages.props) -->
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Scalar.AspNetCore" />
    
    <!-- NServiceBus for ASP.NET Core -->
    <PackageReference Include="NServiceBus" />
    
    <!-- Logging -->
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="Serilog.Sinks.Console" />
    
    <!-- Project references -->
    <ProjectReference Include="..\Domain\Domain.csproj" />
    <ProjectReference Include="..\Infrastructure\Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

**Required Package Versions** (add to `Directory.Packages.props` if missing):

```xml
<PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />
<PackageVersion Include="Scalar.AspNetCore" Version="1.2.53" />
<PackageVersion Include="Serilog.AspNetCore" Version="9.0.0" />
<PackageVersion Include="Serilog.Sinks.Console" Version="6.0.0" />
```

---

## 3. Create Program.cs

Replace the default `Program.cs` with:

```csharp
using Microsoft.Azure.Cosmos;
using NServiceBus;
using YourCompany.ServiceName.Domain.Contracts.Commands;
using YourCompany.ServiceName.Domain.Services.YourDb;
using Scalar.AspNetCore;
using Serilog;
using Infrastructure;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting {ServiceName} API", "YourService");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog();

    // Add controllers
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi();

    // Configure Cosmos DB - Data container
    var cosmosConnectionString = builder.Configuration.GetConnectionString("CosmosDb")
        ?? throw new InvalidOperationException("CosmosDb connection string not configured");

    var databaseName = builder.Configuration["CosmosDb:DatabaseName"] ?? "YourDatabase";
    var containerName = builder.Configuration["CosmosDb:ContainerName"] ?? "YourContainer";

    var cosmosClient = new CosmosClient(cosmosConnectionString);
    var container = cosmosClient.GetContainer(databaseName, containerName);
    builder.Services.AddSingleton(container);

    // Register repositories
    builder.Services.AddSingleton<IYourRepository, YourRepository>();

    // Configure NServiceBus (send-only endpoint with routing)
    builder.Host.NServiceBusEnvironmentConfiguration(
        "YourCompany.ServiceName.Api",
        (config, endpoint, routing) =>
        {
            endpoint.SendOnly();
            
            // Route commands to processing endpoint
            routing.RouteToEndpoint(typeof(YourCommand), "YourCompany.ServiceName.Endpoint");
        });

    var app = builder.Build();

    // Configure middleware
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "{ServiceName} API terminated unexpectedly", "YourService");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
```

**Customization Points**:
- Replace `YourCompany`, `ServiceName`, `YourDatabase`, `YourContainer` with actual names
- Update command routing to your endpoint name
- Add multiple `routing.RouteToEndpoint()` calls for multiple commands

---

## 4. Create appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "NServiceBus": "Information"
    }
  },
  "ConnectionStrings": {
    "ServiceBus": "<<YOUR_SERVICE_BUS_CONNECTION_STRING>>",
    "CosmosDb": "<<YOUR_COSMOS_DB_CONNECTION_STRING>>"
  },
  "CosmosDb": {
    "DatabaseName": "YourDatabase",
    "ContainerName": "YourContainer"
  }
}
```

**Production Configuration** (appsettings.json):
- Use Managed Identity (no connection strings)
- Configure via environment variables or Azure App Configuration

---

## 5. Create launchSettings.json

Create `Properties/launchSettings.json`:

```json
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "launchUrl": "scalar/v1",
      "applicationUrl": "http://localhost:7071",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "Docker": {
      "commandName": "Docker",
      "launchBrowser": true,
      "launchUrl": "{Scheme}://{ServiceHost}:{ServicePort}/scalar/v1",
      "environmentVariables": {
        "ASPNETCORE_URLS": "http://+:8080"
      },
      "publishAllPorts": true,
      "useSSL": false
    }
  }
}
```

**Port Convention**:
- API projects use ports `707X` locally (X = service number)
- Docker/Container Apps use port `8080`

---

## 6. Create Controllers

### Example Controller

Create `Controllers/YourEntityController.cs`:

```csharp
namespace YourCompany.ServiceName.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using NServiceBus;
using YourCompany.ServiceName.Api.Models;
using YourCompany.ServiceName.Domain.Contracts.Commands;

/// <summary>
/// API controller for [your entity] operations
/// </summary>
[ApiController]
[Route("api/[controller-name]")]
[Produces("application/json")]
public class YourEntityController : ControllerBase
{
    private readonly IMessageSession _messageSession;
    private readonly ILogger<YourEntityController> _logger;

    public YourEntityController(
        IMessageSession messageSession,
        ILogger<YourEntityController> logger)
    {
        _messageSession = messageSession ?? throw new ArgumentNullException(nameof(messageSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Performs an action on the entity
    /// </summary>
    /// <param name="request">Request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>202 Accepted - Command queued for processing</returns>
    /// <response code="202">Command accepted and queued</response>
    /// <response code="400">Invalid request data</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PerformAction(
        [FromBody] YourRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid request: {ValidationErrors}",
                string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return BadRequest(ModelState);
        }

        var idempotencyKey = $"{request.EntityId}:{request.UniqueField}";

        var command = new YourCommand(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            EntityId: request.EntityId,
            Field1: request.Field1,
            IdempotencyKey: idempotencyKey
        );

        await _messageSession.Send(command, cancellationToken);

        _logger.LogInformation(
            "Command sent for entity {EntityId}",
            request.EntityId);

        return Accepted(new
        {
            Message = "Command accepted and queued for processing",
            EntityId = request.EntityId
        });
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Service = "YourService API" });
    }
}
```

---

## 7. Create Request/Response Models

Create `Models/YourRequest.cs`:

```csharp
namespace YourCompany.ServiceName.Api.Models;

using System.ComponentModel.DataAnnotations;

public class YourRequest
{
    [Required]
    public string EntityId { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Field1 { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal? Field2 { get; set; }
}
```

---

## 8. Configure OpenAPI/Scalar

OpenAPI configuration is in `Program.cs`:

```csharp
// Add OpenAPI services
builder.Services.AddOpenApi();

// Map OpenAPI endpoints (Development only)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
```

**Access Scalar UI**:
- Local: `http://localhost:7071/scalar/v1`
- OpenAPI spec: `http://localhost:7071/openapi/v1.json`

**Customization** (optional):
```csharp
app.MapScalarApiReference(options =>
{
    options.WithTitle("Your Service API");
    options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});
```

---

## 9. Health Checks (Optional)

Add health check packages:

```xml
<PackageReference Include="AspNetCore.HealthChecks.CosmosDb" />
<PackageReference Include="AspNetCore.HealthChecks.AzureServiceBus" />
```

Update `Program.cs`:

```csharp
// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddAzureServiceBusTopic(
        builder.Configuration.GetConnectionString("ServiceBus"),
        topicName: "bundle-1")
    .AddCosmosDb(
        builder.Configuration.GetConnectionString("CosmosDb"),
        database: builder.Configuration["CosmosDb:DatabaseName"]);

// Map health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Name == "self"
});
```

---

## 10. CORS Configuration

### Development

```csharp
// Program.cs - before var app = builder.Build();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// After var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
}
```

### Production

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

if (app.Environment.IsProduction())
{
    app.UseCors("Production");
}
```

---

## 11. JSON Configuration

```csharp
using System.Text.Json;

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
```

---

## 12. Run and Test

```powershell
# Run API
cd services/<ServiceName>/src/Api
dotnet run

# Test health endpoint
curl http://localhost:7071/health

# Open Scalar UI
# Navigate to http://localhost:7071/scalar/v1
```

---

## 13. Docker Support (Optional)

Create `Dockerfile`:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY ["Api/Api.csproj", "Api/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["Directory.Packages.props", "./"]

# Restore packages
RUN dotnet restore "Api/Api.csproj"

# Copy source code
COPY . .

# Build
WORKDIR "/src/Api"
RUN dotnet build "Api.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create non-root user
RUN groupadd -r apiuser && useradd -r -g apiuser apiuser
USER apiuser

# Copy published files
COPY --from=publish /app/publish .

# Expose port
EXPOSE 8080

# Set environment
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Api.dll"]
```

**Build and run**:
```powershell
docker build -t your-service-api .
docker run -p 8080:8080 your-service-api
```

---

## Common Patterns

### Command Routing

Route all commands from a namespace:
```csharp
builder.Host.NServiceBusEnvironmentConfiguration(
    "YourCompany.ServiceName.Api",
    (config, endpoint, routing) =>
    {
        endpoint.SendOnly();
        
        // Route by type
        routing.RouteToEndpoint(typeof(CreateOrder), "Orders.Endpoint");
        routing.RouteToEndpoint(typeof(UpdateOrder), "Orders.Endpoint");
        
        // Or route by assembly
        routing.RouteToEndpoint(
            assembly: typeof(CreateOrder).Assembly,
            destination: "Orders.Endpoint");
    });
```

### Multiple Repositories

```csharp
// Register multiple repositories
builder.Services.AddSingleton<IOrderRepository, OrderRepository>();
builder.Services.AddSingleton<ICustomerRepository, CustomerRepository>();
builder.Services.AddSingleton<IProductRepository, ProductRepository>();
```

### Environment-Specific Configuration

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseCors("Development");
}
else if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseCors("Production");
}
```

---

## Troubleshooting

### NServiceBus Routing Errors

**Error**: `No destination specified for message: YourCommand`

**Solution**: Add routing configuration:
```csharp
routing.RouteToEndpoint(typeof(YourCommand), "YourCompany.ServiceName.Endpoint");
```

### OpenAPI 404 Errors

**Error**: `/openapi/v1.json` returns 404

**Solution**: Ensure `AddOpenApi()` and `MapOpenApi()` are called:
```csharp
builder.Services.AddOpenApi();
app.MapOpenApi();
```

### Cosmos DB Connection Errors

**Error**: Connection string not configured

**Solution**: Set in `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "CosmosDb": "AccountEndpoint=https://...;AccountKey=..."
  }
}
```

---

## Next Steps

1. **Create Endpoint.In project** for message processing (see `init.endpoint.md`)
2. **Add authentication** (JWT, OAuth2, etc.)
3. **Add rate limiting** (AspNetCoreRateLimit package)
4. **Set up CI/CD** for deployment
5. **Configure Application Insights** for monitoring

---

## Checklist

- [ ] Project created with correct naming
- [ ] Package references added
- [ ] Program.cs configured
- [ ] appsettings.Development.json created
- [ ] launchSettings.json configured
- [ ] Controller(s) created
- [ ] Request/Response models defined
- [ ] NServiceBus routing configured
- [ ] Cosmos DB container registered
- [ ] Health checks added (optional)
- [ ] CORS configured
- [ ] JSON options configured
- [ ] Tested locally with `dotnet run`
- [ ] Verified Scalar UI works
- [ ] Dockerfile created (if using containers)

---

**Reference**: See [api-conventions.md](api-conventions.md) for API design patterns and best practices.
