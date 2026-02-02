# ⚠️ LOGGING - TO BE DOCUMENTED

## Status: PLACEHOLDER

**This file is a placeholder for logging standards and practices that need to be documented.**

When documenting this section, include:

## Topics to Cover

### Logging Framework
- Microsoft.Extensions.Logging usage
- Serilog integration (if used)
- Configuration in Program.cs
- Log providers (Console, Application Insights, etc.)

### Log Levels
- **Trace**: Very detailed diagnostic information
- **Debug**: Internal system events for debugging
- **Information**: General application flow
- **Warning**: Abnormal but expected events
- **Error**: Errors and exceptions
- **Critical**: Critical failures requiring immediate attention

### When to Log at Each Level
```csharp
// Examples needed for:
_logger.LogTrace("...");     // When to use
_logger.LogDebug("...");     // When to use
_logger.LogInformation("..."); // When to use
_logger.LogWarning("...");   // When to use
_logger.LogError("...");     // When to use
_logger.LogCritical("...");  // When to use
```

### Structured Logging
- Using structured log messages
- Log message templates
- Including context properties
- Avoid string interpolation in log messages

```csharp
// Good: Structured logging
_logger.LogInformation(
    "Processing event {EventId} with status {Status}", 
    eventId, 
    status);

// Bad: String interpolation
_logger.LogInformation($"Processing event {eventId} with status {status}");
```

### Correlation IDs
- Request correlation across services
- NServiceBus message correlation
- Tracking distributed transactions
- Including correlation ID in logs

### PII Handling
- What is considered PII
- Never log passwords, tokens, or secrets
- Masking or hashing sensitive data
- GDPR and compliance considerations
- Sanitization strategies

### Log Categories
- Using category names (typically class name)
- Filtering by category
- Namespace-based filtering

### Application Insights Integration
- Configuration for Azure
- Custom telemetry
- Performance tracking
- Dependency tracking
- Exception tracking

### Performance Considerations
- Avoid expensive operations in log messages
- Use log level checks for expensive formatting
- Async logging considerations

```csharp
// Check log level before expensive operations
if (_logger.IsEnabled(LogLevel.Debug))
{
    var expensiveData = ComputeExpensiveData();
    _logger.LogDebug("Expensive data: {Data}", expensiveData);
}
```

### Log Message Guidelines
- Be specific and actionable
- Include relevant context
- Use consistent message format
- Avoid excessive logging

### Example Patterns Needed

```csharp
// API Logging
public class EventsController : ControllerBase
{
    private readonly ILogger<EventsController> _logger;
    
    [HttpPost]
    public async Task<IActionResult> CreateEvent(CreateEventRequest request)
    {
        _logger.LogInformation("Creating event: {EventName}", request.Name);
        // To be documented
    }
}

// Handler Logging
public class CreateEventCommandHandler : IHandleMessages<CreateEventCommand>
{
    private readonly ILogger<CreateEventCommandHandler> _logger;
    
    public async Task Handle(CreateEventCommand message, IMessageHandlerContext context)
    {
        _logger.LogInformation("Handling CreateEventCommand for event {EventId}", message.EventId);
        // To be documented
    }
}

// Repository Logging
public class EventRepository : IEventRepository
{
    private readonly ILogger<EventRepository> _logger;
    
    public async Task SaveAsync(Event entity)
    {
        _logger.LogDebug("Saving event {EventId} to Cosmos DB", entity.Id);
        // To be documented
    }
}
```

### Configuration Examples Needed

```json
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "EventManagement": "Debug"
    }
  }
}
```

### Testing Logging
- Verifying log messages in tests
- Using test loggers
- Asserting on log output

## Action Required

**This section needs to be filled out with:**
1. Review existing logging in the codebase
2. Document current logging configuration
3. Define logging standards for new code
4. Provide comprehensive examples
5. Document Application Insights setup
6. Create guidelines for what to log and when

**Priority**: High - Logging is essential for observability and debugging

**Owner**: To be assigned

**Related Files**:
- See [error-handling.md](error-handling.md) for error logging specifics
- See [api-conventions.md](api-conventions.md) for API request logging
- See [messaging-patterns.md](messaging-patterns.md) for message handler logging
