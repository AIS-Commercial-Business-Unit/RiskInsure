# ⚠️ HEALTH CHECKS - TO BE DOCUMENTED

## Status: PLACEHOLDER

**This file is a placeholder for health check implementation details that need to be documented.**

When documenting this section, include:

## Topics to Cover

### Health Check Endpoints
- `/health` - Overall health
- `/health/ready` - Readiness probe (dependencies available)
- `/health/live` - Liveness probe (process running)
- Custom health check endpoints

### ASP.NET Core Health Checks
- Configuration in Program.cs
- Built-in health checks
- Custom health checks
- Health check middleware

```csharp
// Example configuration needed
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddAzureServiceBusQueue(...)
    .AddCosmosDb(...);

app.MapHealthChecks("/health");
```

### Dependencies to Check
- **Self**: Basic liveness (process running)
- **Cosmos DB**: Database connectivity and authentication
- **Azure Service Bus**: Message queue connectivity
- **External Services**: Any external APIs or services
- **Storage**: Azure Storage if used

### Health Check Types

#### Liveness Probe
- Determines if application should be restarted
- Simple check (is process running)
- Fast and lightweight
- Used by container orchestrators

#### Readiness Probe
- Determines if application can accept traffic
- Checks all dependencies
- Can take longer
- Used for load balancer routing

#### Startup Probe
- Checks if application has started
- Gives slow-starting apps more time
- Prevents premature restarts

### Custom Health Checks
```csharp
// Example custom health check
public class CosmosDbHealthCheck : IHealthCheck
{
    private readonly CosmosClient _client;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Perform health check logic
            // To be documented
            return HealthCheckResult.Healthy("Cosmos DB is responding");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cosmos DB is unavailable", ex);
        }
    }
}
```

### Health Check Responses
- JSON response format
- Status codes (200 OK, 503 Service Unavailable)
- Detailed vs summary responses
- Security considerations (what to expose)

```json
// Example response format
{
  "status": "Healthy",
  "checks": {
    "self": {
      "status": "Healthy"
    },
    "cosmos_db": {
      "status": "Healthy",
      "description": "Cosmos DB is responding"
    },
    "service_bus": {
      "status": "Degraded",
      "description": "High queue depth",
      "data": {
        "queueDepth": 1000
      }
    }
  },
  "totalDuration": "00:00:00.1234567"
}
```

### Health Check Tags
- Tagging health checks (e.g., "ready", "live")
- Filtering health checks by tag
- Different endpoints for different tags

### Integration with Azure Container Apps
- Configuring health probes
- Probe intervals and timeouts
- Failure thresholds
- Initial delay seconds

```yaml
# Example Container App configuration
healthProbes:
  - type: liveness
    httpGet:
      path: /health/live
      port: 5271
    initialDelaySeconds: 10
    periodSeconds: 30
  - type: readiness
    httpGet:
      path: /health/ready
      port: 5271
    initialDelaySeconds: 5
    periodSeconds: 10
```

### Performance Considerations
- Keep health checks fast
- Use caching for expensive checks
- Set appropriate timeouts
- Avoid cascading health check calls

### Degraded Status
- When to return Degraded vs Unhealthy
- Partial functionality scenarios
- Graceful degradation patterns

### Monitoring Health Checks
- Logging health check failures
- Alerting on repeated failures
- Tracking health check duration
- Integration with monitoring tools

### Testing Health Checks
- Unit testing custom health checks
- Integration testing with real dependencies
- Simulating failure scenarios
- Testing timeout behavior

```csharp
// Test example needed
public class CosmosDbHealthCheckTests
{
    [Fact]
    public async Task CheckHealth_WhenCosmosResponds_ReturnsHealthy()
    {
        // To be documented
    }
    
    [Fact]
    public async Task CheckHealth_WhenCosmosUnavailable_ReturnsUnhealthy()
    {
        // To be documented
    }
}
```

## Example Implementations Needed

### Basic Health Check Setup
```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddCheck<CosmosDbHealthCheck>("cosmos_db")
    .AddCheck<ServiceBusHealthCheck>("service_bus");

var app = builder.Build();

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

### Custom Health Check with Caching
```csharp
// Avoid checking dependency too frequently
public class CachedHealthCheck : IHealthCheck
{
    private readonly IHealthCheck _innerCheck;
    private readonly TimeSpan _cacheDuration;
    private HealthCheckResult _cachedResult;
    private DateTime _lastCheck;
    
    public async Task<HealthCheckResult> CheckHealthAsync(...)
    {
        // Caching logic to be documented
    }
}
```

## Action Required

**This section needs to be filled out with:**
1. Document existing health check configuration
2. Define health check standards
3. Implement comprehensive health checks for all dependencies
4. Document Container Apps probe configuration
5. Provide testing examples
6. Document monitoring and alerting setup
7. Create troubleshooting guide

**Priority**: High - Health checks are critical for production reliability

**Owner**: To be assigned

**Related Files**:
- See [api-conventions.md](api-conventions.md) for API health endpoints
- See [deployment.md](deployment.md) for Container Apps configuration
- See [technology-stack.md](technology-stack.md) for health check packages
