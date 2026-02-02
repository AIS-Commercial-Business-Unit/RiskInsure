# API Conventions

## Overview

This document defines the conventions and patterns for building RESTful HTTP APIs using ASP.NET Core. The API layer serves as the HTTP interface to the bounded context, handling requests and publishing commands to the message bus.

## RESTful Design Principles

### Resource-Oriented URLs
Use nouns, not verbs:
```
✅ GET    /api/events/{id}
✅ POST   /api/events
✅ PUT    /api/events/{id}
✅ DELETE /api/events/{id}

❌ GET    /api/getEvent?id=123
❌ POST   /api/createEvent
```

### HTTP Methods
- **GET**: Retrieve resource(s), idempotent, no side effects
- **POST**: Create new resource or trigger action
- **PUT**: Update entire resource, idempotent
- **PATCH**: Partial update
- **DELETE**: Remove resource, idempotent

### Standard HTTP Status Codes

**Success Codes**:
- `200 OK`: Successful GET, PUT, PATCH, or synchronous operation
- `201 Created`: Resource created, include `Location` header
- `202 Accepted`: **Primary pattern - async command accepted**
- `204 No Content`: Successful DELETE or operation with no return value

**Client Error Codes**:
- `400 Bad Request`: Invalid request format, validation failure
- `401 Unauthorized`: Authentication required
- `403 Forbidden`: Authenticated but not authorized
- `404 Not Found`: Resource doesn't exist
- `409 Conflict`: Concurrent modification, business rule violation
- `422 Unprocessable Entity`: Semantic validation failure

**Server Error Codes**:
- `500 Internal Server Error`: Unexpected server error
- `503 Service Unavailable`: Temporary unavailability

## Async Processing Pattern

### Standard Pattern
Most operations use **fire-and-forget** with 202 Accepted:

```csharp
[HttpPost]
[Route("api/events")]
public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
{
    // 1. Validate request format
    if (!ModelState.IsValid)
        return BadRequest(ModelState);
    
    // 2. Send command to message bus
    await _messageSession.Send(new CreateEventCommand
    {
        EventId = Guid.NewGuid(),
        Name = request.Name,
        StartDate = request.StartDate,
        EndDate = request.EndDate
    });
    
    // 3. Return 202 Accepted
    return Accepted(new 
    { 
        message = "Event creation initiated",
        eventId = eventId
    });
}
```

### Why 202 Accepted?
- Acknowledges request receipt
- Indicates async processing
- Doesn't wait for completion
- Scales better under load
- Prevents timeout issues

### When to Use 200 OK
Use synchronous responses only when:
- Simple queries with no side effects
- Data immediately available
- No complex business logic
- Low latency operations

```csharp
[HttpGet]
[Route("api/events/{id}")]
public async Task<IActionResult> GetEvent(Guid id)
{
    var evt = await _repository.GetByIdAsync(id);
    
    if (evt == null)
        return NotFound();
    
    return Ok(new EventResponse
    {
        Id = evt.Id,
        Name = evt.Name,
        Status = evt.Status.ToString()
    });
}
```

## Request/Response Models

### Request DTOs
```csharp
// API/Models/CreateEventRequest.cs
public class CreateEventRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; }
    
    [Required]
    public DateTime StartDate { get; set; }
    
    [Required]
    public DateTime EndDate { get; set; }
}
```

### Response DTOs
```csharp
// API/Models/EventResponse.cs
public class EventResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Status { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
```

### Best Practices
- Separate request/response models from domain entities
- Use Data Annotations for basic validation
- Keep DTOs simple (no logic)
- Use appropriate data types
- Document with XML comments

## Validation

### Two-Layer Validation

**Layer 1: API (Format Validation)**
```csharp
[HttpPost]
public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
{
    // Automatic model validation
    if (!ModelState.IsValid)
        return BadRequest(ModelState);
    
    // Additional format checks
    if (request.EndDate <= request.StartDate)
        return BadRequest("End date must be after start date");
    
    // Send to message bus
    await _messageSession.Send(new CreateEventCommand { /* ... */ });
    return Accepted();
}
```

**Layer 2: Domain (Business Validation)**
- Performed in message handlers
- Validates business rules
- Ensures invariants
- Failures published as events

### Validation Response Format
```json
{
  "errors": {
    "Name": ["The Name field is required."],
    "EndDate": ["End date must be after start date"]
  }
}
```

## Error Handling

### Exception Filter
```csharp
public class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;
    
    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, "API error occurred");
        
        var result = context.Exception switch
        {
            ValidationException ex => new BadRequestObjectResult(new { error = ex.Message }),
            NotFoundException ex => new NotFoundObjectResult(new { error = ex.Message }),
            _ => new ObjectResult(new { error = "An error occurred" }) 
                { StatusCode = 500 }
        };
        
        context.Result = result;
        context.ExceptionHandled = true;
    }
}
```

### Error Response Format
```json
{
  "error": "Detailed error message",
  "traceId": "correlation-id-123"
}
```

## API Versioning

### URL-Based Versioning
```
/api/v1/events
/api/v2/events
```

### Implementation
```csharp
[ApiController]
[Route("api/v1/events")]
public class EventsV1Controller : ControllerBase
{
    // V1 endpoints
}

[ApiController]
[Route("api/v2/events")]
public class EventsV2Controller : ControllerBase
{
    // V2 endpoints with breaking changes
}
```

### Version Strategy
- Start with v1
- Maintain backwards compatibility when possible
- Create new version for breaking changes
- Deprecate old versions gradually

## Swagger/OpenAPI

### Configuration
```csharp
// Program.cs
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Event Management API",
        Version = "v1",
        Description = "API for managing events in the AcmeTickets system"
    });
    
    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Event Management API v1");
});
```

### XML Documentation
```csharp
/// <summary>
/// Creates a new event
/// </summary>
/// <param name="request">Event details</param>
/// <returns>Accepted response with event ID</returns>
/// <response code="202">Event creation initiated</response>
/// <response code="400">Invalid request</response>
[HttpPost]
[ProducesResponseType(StatusCodes.Status202Accepted)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
{
    // Implementation
}
```

## Health Checks

### Implementation
```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddAzureServiceBusQueue(
        builder.Configuration["ServiceBus:ConnectionString"],
        queueName: "event-commands")
    .AddCosmosDb(
        builder.Configuration["CosmosDb:ConnectionString"],
        database: "EventManagementDb");

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

### Health Check Endpoints
- `/health`: Overall health
- `/health/ready`: Readiness probe (dependencies available)
- `/health/live`: Liveness probe (process running)

## CORS Configuration

### Development
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

app.UseCors("Development");
```

### Production
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins("https://acmetickets.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

app.UseCors("Production");
```

## Authentication & Authorization

### Placeholder for Future Implementation
```csharp
// To be implemented based on requirements
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // JWT configuration
    });

builder.Services.AddAuthorization(options =>
{
    // Authorization policies
});

app.UseAuthentication();
app.UseAuthorization();
```

### Securing Endpoints
```csharp
[Authorize]
[HttpPost]
[Route("api/events")]
public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
{
    // Only authenticated users
}

[Authorize(Policy = "AdminOnly")]
[HttpDelete]
[Route("api/events/{id}")]
public async Task<IActionResult> DeleteEvent(Guid id)
{
    // Only administrators
}
```

## Logging

### Request Logging
```csharp
[HttpPost]
public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
{
    _logger.LogInformation(
        "Creating event: {EventName} from {StartDate} to {EndDate}",
        request.Name, 
        request.StartDate, 
        request.EndDate);
    
    try
    {
        await _messageSession.Send(command);
        _logger.LogInformation("Event creation command sent: {EventId}", eventId);
        return Accepted();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to send event creation command");
        throw;
    }
}
```

### Log Levels
- **Debug**: Detailed diagnostic information
- **Information**: General flow (requests, commands sent)
- **Warning**: Unexpected but handled situations
- **Error**: Failures and exceptions
- **Critical**: System failures requiring immediate attention

## Content Negotiation

### JSON Default
```csharp
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
```

### Multiple Formats (if needed)
```csharp
[HttpGet]
[Produces("application/json", "application/xml")]
public IActionResult GetEvent(Guid id)
{
    // Returns JSON or XML based on Accept header
}
```

## Rate Limiting

### Placeholder for Future Implementation
```csharp
// Consider using AspNetCoreRateLimit package
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Limit = 100,
            Period = "1m"
        }
    };
});
```

## Best Practices

### Do's ✅
1. Use 202 Accepted for async operations
2. Validate at API boundary (format/structure)
3. Publish commands, don't call handlers directly
4. Return meaningful error messages
5. Document endpoints with Swagger
6. Implement health checks
7. Log requests and errors
8. Use DTOs (don't expose domain entities)
9. Handle exceptions gracefully
10. Version breaking API changes

### Don'ts ❌
1. Don't include business logic in controllers
2. Don't query database directly from API
3. Don't wait for message processing to complete
4. Don't expose internal implementation details
5. Don't return stack traces in production
6. Don't ignore validation
7. Don't use synchronous processing for complex operations
8. Don't couple API to domain entities
9. Don't forget CORS in production
10. Don't log sensitive data (PII, credentials)

## Example Complete Endpoint

```csharp
/// <summary>
/// Creates a new event
/// </summary>
[HttpPost]
[Route("api/events")]
[ProducesResponseType(typeof(CreateEventResponse), StatusCodes.Status202Accepted)]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> CreateEvent(
    [FromBody] CreateEventRequest request,
    CancellationToken cancellationToken)
{
    _logger.LogInformation("Received event creation request: {EventName}", request.Name);
    
    // Format validation
    if (!ModelState.IsValid)
    {
        _logger.LogWarning("Invalid event creation request");
        return BadRequest(ModelState);
    }
    
    if (request.EndDate <= request.StartDate)
    {
        _logger.LogWarning("Invalid date range: End date must be after start date");
        return BadRequest(new { error = "End date must be after start date" });
    }
    
    // Generate ID
    var eventId = Guid.NewGuid();
    
    // Send command
    var command = new CreateEventCommand
    {
        EventId = eventId,
        Name = request.Name,
        StartDate = request.StartDate,
        EndDate = request.EndDate
    };
    
    await _messageSession.Send(command, cancellationToken);
    
    _logger.LogInformation("Event creation command sent: {EventId}", eventId);
    
    // Return 202 Accepted
    return Accepted(new CreateEventResponse
    {
        EventId = eventId,
        Message = "Event creation initiated"
    });
}
```

## Testing APIs

### Integration Tests
```csharp
public class EventsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    
    [Fact]
    public async Task CreateEvent_ValidRequest_Returns202()
    {
        // Arrange
        var request = new CreateEventRequest
        {
            Name = "Test Event",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7)
        };
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/events", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
```
