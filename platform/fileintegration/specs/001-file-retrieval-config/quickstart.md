# Quickstart: Manual File Check Trigger API

**Feature**: 001-file-retrieval-config  
**Date**: 2025-01-24  
**Audience**: Developers implementing similar manual trigger features

---

## Overview

This quickstart demonstrates how to add a manual trigger API endpoint that sends an existing command with enhanced audit trail via events. The pattern shown here is reusable for any scenario where you need to expose command-sending capabilities via REST API with security trimming.

**What This Guide Covers**:
- Adding a trigger endpoint to an existing controller
- Validating entity existence and ownership before sending commands
- Publishing audit events at the start of message processing
- Ensuring idempotency in event publishing
- Testing manual trigger functionality

**Prerequisites**:
- Existing command and handler infrastructure
- JWT authentication configured
- Entity repository for validation
- NServiceBus configured in API and Worker projects

---

## Architecture Pattern

### Manual Trigger Flow

```text
┌─────────┐                 ┌─────────┐                 ┌─────────┐
│   API   │ ─── Command ───→│ Message │ ─── Command ───→│ Handler │
│         │                 │   Bus   │                 │         │
└─────────┘                 └─────────┘                 └────┬────┘
     ↓                                                        ↓
  Return 202                                          Publish Event
  Immediately                                         (Audit Trail)
                                                             ↓
                                                      Execute Business
                                                          Logic
```

**Key Principles**:
1. **API validates before sending** - Fast-fail for invalid requests (don't waste message processing)
2. **Handler publishes event first** - Capture intention before execution (audit trail)
3. **Idempotency via stable ExecutionId** - Generate once in handler, use across retries
4. **Security trimming in query** - Repository filters by clientId (no cross-tenant access)
5. **Async response pattern** - Return 202 Accepted immediately (don't wait for completion)

---

## Implementation Steps

### Step 1: Add Event Contract

**File**: `src/{Service}.Contracts/Events/{Action}Triggered.cs`

```csharp
using NServiceBus;

namespace {Service}.Contracts.Events;

/// <summary>
/// Event published when {action} is triggered.
/// Captures trigger source and context for audit trail.
/// </summary>
public record {Action}Triggered : IEvent
{
    // Standard message metadata (ALWAYS include these)
    public Guid MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset OccurredUtc { get; init; }
    public required string IdempotencyKey { get; init; }
    
    // Domain context (what was triggered)
    public required string ClientId { get; init; }
    public required Guid EntityId { get; init; }
    public required string EntityName { get; init; }
    
    // Execution tracking (how to track progress)
    public required Guid ExecutionId { get; init; }
    
    // Trigger context (who and why)
    public required bool IsManualTrigger { get; init; }
    public required string TriggeredBy { get; init; }
}
```

**Naming Convention**: `{Noun}{VerbPastTense}` → `FileCheckTriggered`, `PaymentTriggered`, `ReportTriggered`

---

### Step 2: Modify Existing Command (Optional)

**File**: `src/{Service}.Contracts/Commands/{Action}.cs`

If your existing command doesn't track trigger source, add optional fields:

```csharp
public record ExecuteAction : ICommand
{
    // ... existing fields ...
    
    // ✨ ADD: Track trigger source
    public bool IsManualTrigger { get; init; }
    public string? TriggeredBy { get; init; }  // Nullable for backward compatibility
}
```

**Why Optional Fields**:
- Backward compatible with existing scheduled senders
- Manual triggers populate both fields
- Handler can distinguish trigger source

---

### Step 3: Add API Endpoint

**File**: `src/{Service}.API/Controllers/{Entity}Controller.cs`

```csharp
/// <summary>
/// Manually triggers {action} for the specified {entity}.
/// </summary>
[HttpPost("{entityId}/trigger")]
[Authorize(Policy = "ClientAccess")]
[ProducesResponseType(typeof(Trigger{Action}Response), StatusCodes.Status202Accepted)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> Trigger{Action}(
    [FromRoute] Guid entityId,
    CancellationToken cancellationToken)
{
    try
    {
        // 1. Extract security context
        var clientId = GetClientIdFromClaims();
        var userId = GetUserIdFromClaims();
        
        _logger.LogInformation(
            "Triggering {action} for {entity} {EntityId} (Client: {ClientId}, User: {UserId})",
            "{action}",
            "{entity}",
            entityId,
            clientId,
            userId);
        
        // 2. Validate entity exists and user has access
        var entity = await _{service}.GetByIdAsync(clientId, entityId, cancellationToken);
        if (entity == null)
        {
            _logger.LogWarning(
                "{Entity} {EntityId} not found for client {ClientId}",
                "{Entity}",
                entityId,
                clientId);
            return NotFound(new { error = "{Entity} not found or access denied" });
        }
        
        // 3. Validate entity state (e.g., active, not processing, etc.)
        if (!entity.IsActive)
        {
            _logger.LogWarning(
                "{Entity} {EntityId} is inactive",
                "{Entity}",
                entityId);
            return BadRequest(new { error = "{Entity} is inactive and cannot be triggered" });
        }
        
        // 4. Generate execution ID (for tracking and idempotency)
        var executionId = Guid.NewGuid();
        var correlationId = $"manual-trigger-{clientId}-{entityId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        
        // 5. Send command
        await _messageSession.Send(new Execute{Action}
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = correlationId,
            OccurredUtc = DateTimeOffset.UtcNow,
            IdempotencyKey = $"{clientId}:{entityId}:{executionId}",
            ClientId = clientId,
            EntityId = entityId,
            IsManualTrigger = true,
            TriggeredBy = userId
        });
        
        // 6. Return 202 Accepted immediately
        var response = new Trigger{Action}Response
        {
            EntityId = entityId,
            ExecutionId = executionId,
            TriggeredAt = DateTimeOffset.UtcNow,
            Message = $"{Action} triggered successfully. Use executionId to track progress."
        };
        
        _logger.LogInformation(
            "{Action} triggered successfully for {entity} {EntityId} (ExecutionId: {ExecutionId})",
            "{action}",
            "{entity}",
            entityId,
            executionId);
        
        return Accepted(response);
    }
    catch (UnauthorizedAccessException ex)
    {
        _logger.LogWarning(ex, "Unauthorized trigger attempt for {entity} {EntityId}", "{entity}", entityId);
        return Unauthorized(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error triggering {action} for {entity} {EntityId}", "{action}", "{entity}", entityId);
        return StatusCode(500, new { error = "Failed to trigger {action}. Please try again." });
    }
}
```

**Key Points**:
- ✅ Validate before sending command (fast-fail)
- ✅ Generate ExecutionId in API (not in handler) - **NOTE**: See Step 4 correction below
- ✅ Return 202 Accepted (async pattern)
- ✅ Include tracking ID in response
- ✅ Security trimming via service call

**⚠️ CORRECTION**: Based on research findings (R7), ExecutionId should be generated in **handler**, not API, to ensure idempotency across handler retries. API should NOT generate ExecutionId. See Step 4 for corrected pattern.

---

### Step 3 (Corrected): Add API Endpoint - Simplified Version

**File**: `src/{Service}.API/Controllers/{Entity}Controller.cs`

```csharp
/// <summary>
/// Manually triggers {action} for the specified {entity}.
/// </summary>
[HttpPost("{entityId}/trigger")]
[Authorize(Policy = "ClientAccess")]
[ProducesResponseType(StatusCodes.Status202Accepted)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> Trigger{Action}(
    [FromRoute] Guid entityId,
    CancellationToken cancellationToken)
{
    try
    {
        // 1. Extract security context
        var clientId = GetClientIdFromClaims();
        var userId = GetUserIdFromClaims();
        
        // 2. Validate entity exists and user has access
        var entity = await _{service}.GetByIdAsync(clientId, entityId, cancellationToken);
        if (entity == null)
        {
            return NotFound(new { error = "{Entity} not found or access denied" });
        }
        
        // 3. Validate entity state
        if (!entity.IsActive)
        {
            return BadRequest(new { error = "{Entity} is inactive" });
        }
        
        // 4. Send command (ExecutionId generated in handler)
        await _messageSession.Send(new Execute{Action}
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = $"manual-trigger-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            OccurredUtc = DateTimeOffset.UtcNow,
            IdempotencyKey = $"{clientId}:{entityId}:{DateTimeOffset.UtcNow.Ticks}",
            ClientId = clientId,
            EntityId = entityId,
            IsManualTrigger = true,
            TriggeredBy = userId
        });
        
        // 5. Return 202 Accepted (ExecutionId not available until handler processes)
        return Accepted(new { 
            message = "{Action} triggered successfully",
            entityId = entityId
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error triggering {action}");
        return StatusCode(500, new { error = "Failed to trigger {action}" });
    }
}
```

**Simplified Approach**:
- API doesn't generate or return ExecutionId
- Handler generates ExecutionId for event publishing and service call
- Response confirms acceptance without execution tracking ID
- Client can query execution history by configurationId to find latest execution

**Trade-off**: Less detailed immediate feedback vs simpler implementation and better idempotency guarantees.

---

### Step 4: Modify Handler to Publish Event

**File**: `src/{Service}.Application/MessageHandlers/Execute{Action}Handler.cs`

```csharp
public class Execute{Action}Handler : IHandleMessages<Execute{Action}>
{
    private readonly I{Service} _{service};
    private readonly I{Entity}Repository _{repository};
    private readonly ILogger<Execute{Action}Handler> _logger;

    public async Task Handle(Execute{Action} message, IMessageHandlerContext context)
    {
        _logger.LogInformation(
            "Handling Execute{Action} command for {entity} {EntityId} (CorrelationId: {CorrelationId})",
            "{entity}",
            message.EntityId,
            message.CorrelationId);

        try
        {
            // 1. Load entity
            var entity = await _{repository}.GetByIdAsync(
                message.ClientId,
                message.EntityId,
                CancellationToken.None);

            if (entity == null)
            {
                _logger.LogWarning("{Entity} {EntityId} not found", "{entity}", message.EntityId);
                return; // Already logged, skip processing
            }

            // 2. Validate state
            if (!entity.IsActive)
            {
                _logger.LogWarning("{Entity} {EntityId} is inactive, skipping", "{entity}", message.EntityId);
                return;
            }

            // 3. ✨ Generate ExecutionId and publish event BEFORE processing
            var executionId = Guid.NewGuid();
            
            await context.Publish(new {Action}Triggered
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = message.CorrelationId,
                OccurredUtc = DateTimeOffset.UtcNow,
                IdempotencyKey = $"{message.ClientId}:{message.EntityId}:triggered:{executionId}",
                ClientId = message.ClientId,
                EntityId = message.EntityId,
                EntityName = entity.Name,
                ExecutionId = executionId,
                IsManualTrigger = message.IsManualTrigger,
                TriggeredBy = message.TriggeredBy ?? "Scheduler"
            });

            _logger.LogInformation(
                "Published {Action}Triggered event (ExecutionId: {ExecutionId}, TriggeredBy: {TriggeredBy})",
                executionId,
                message.TriggeredBy ?? "Scheduler");

            // 4. Execute business logic (pass executionId)
            var result = await _{service}.Execute{Action}Async(
                entity,
                executionId,
                CancellationToken.None);

            // 5. Publish completion events (existing pattern)
            if (result.Success)
            {
                await context.Publish(new {Action}Completed { /* ... */ });
            }
            else
            {
                await context.Publish(new {Action}Failed { /* ... */ });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Execute{Action} command");
            
            // Publish failure event
            await context.Publish(new {Action}Failed { /* ... */ });
            
            throw; // Re-throw for NServiceBus retry
        }
    }
}
```

**Critical Pattern**: Generate `ExecutionId` in handler, not in API or service. This ensures:
- Idempotency: Same ExecutionId across handler retries
- Event deduplication: Stable IdempotencyKey
- Tracking: ExecutionId available for all subsequent operations

---

## Step-by-Step Implementation

### 1. Design Phase Checklist

- [ ] Identify existing command to expose via API
- [ ] Determine what entity the command operates on
- [ ] Verify command has trigger source fields (`IsManualTrigger`, `TriggeredBy`) or add them
- [ ] Define event contract for audit trail (`{Action}Triggered`)
- [ ] Design API response DTO (`Trigger{Action}Response`)
- [ ] Identify validation rules (entity exists, active, ownership, etc.)

### 2. Create Event Contract

**File**: `src/{Service}.Contracts/Events/{Action}Triggered.cs`

**Template**:
```csharp
public record {Action}Triggered : IEvent
{
    // Standard metadata
    public Guid MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset OccurredUtc { get; init; }
    public required string IdempotencyKey { get; init; }
    
    // Domain context (customize for your domain)
    public required string ClientId { get; init; }
    public required Guid EntityId { get; init; }
    public required string EntityName { get; init; }
    
    // Execution tracking
    public required Guid ExecutionId { get; init; }
    
    // Trigger context
    public required bool IsManualTrigger { get; init; }
    public required string TriggeredBy { get; init; }
}
```

### 3. Create Response DTO

**File**: `src/{Service}.API/Models/Trigger{Action}Response.cs`

**Template**:
```csharp
namespace {Namespace}.API.Models;

/// <summary>
/// Response returned when {action} is manually triggered.
/// </summary>
public record Trigger{Action}Response
{
    public required Guid EntityId { get; init; }
    public required DateTimeOffset TriggeredAt { get; init; }
    public required string Message { get; init; }
}
```

**Note**: Simplified response without ExecutionId (see trade-offs section).

### 4. Add API Endpoint to Existing Controller

**File**: `src/{Service}.API/Controllers/{Entity}Controller.cs`

**Pattern**:
```csharp
[HttpPost("{entityId}/trigger")]
[Authorize(Policy = "ClientAccess")]
public async Task<IActionResult> Trigger{Action}([FromRoute] Guid entityId)
{
    var clientId = GetClientIdFromClaims();
    var userId = GetUserIdFromClaims();
    
    // Validate
    var entity = await _service.GetByIdAsync(clientId, entityId, CancellationToken.None);
    if (entity == null) return NotFound(new { error = "Not found or access denied" });
    if (!entity.IsActive) return BadRequest(new { error = "Entity is inactive" });
    
    // Send command
    await _messageSession.Send(new Execute{Action}
    {
        MessageId = Guid.NewGuid(),
        CorrelationId = $"manual-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
        OccurredUtc = DateTimeOffset.UtcNow,
        IdempotencyKey = $"{clientId}:{entityId}:{DateTimeOffset.UtcNow.Ticks}",
        ClientId = clientId,
        EntityId = entityId,
        IsManualTrigger = true,
        TriggeredBy = userId
    });
    
    // Return 202
    return Accepted(new Trigger{Action}Response
    {
        EntityId = entityId,
        TriggeredAt = DateTimeOffset.UtcNow,
        Message = "{Action} triggered successfully"
    });
}
```

### 5. Modify Handler to Publish Event

**File**: `src/{Service}.Application/MessageHandlers/Execute{Action}Handler.cs`

**Add at start of processing** (after loading entity, before business logic):

```csharp
public async Task Handle(Execute{Action} message, IMessageHandlerContext context)
{
    // Load entity
    var entity = await _repository.GetByIdAsync(message.ClientId, message.EntityId, CancellationToken.None);
    if (entity == null) return; // Skip if not found
    if (!entity.IsActive) return; // Skip if inactive
    
    // ✨ Generate ExecutionId and publish event BEFORE processing
    var executionId = Guid.NewGuid();
    
    await context.Publish(new {Action}Triggered
    {
        MessageId = Guid.NewGuid(),
        CorrelationId = message.CorrelationId,
        OccurredUtc = DateTimeOffset.UtcNow,
        IdempotencyKey = $"{message.ClientId}:{message.EntityId}:triggered:{executionId}",
        ClientId = message.ClientId,
        EntityId = message.EntityId,
        EntityName = entity.Name,
        ExecutionId = executionId,
        IsManualTrigger = message.IsManualTrigger,
        TriggeredBy = message.TriggeredBy ?? "Scheduler"
    });
    
    // Continue with existing processing...
    var result = await _service.ExecuteAsync(entity, executionId, CancellationToken.None);
    // ...
}
```

### 6. Update Service to Accept ExecutionId

**File**: `src/{Service}.Application/Services/{Service}.cs`

**Change signature**:
```csharp
// Before:
public async Task<Result> ExecuteAsync(Entity entity, CancellationToken ct)
{
    var executionId = Guid.NewGuid(); // Generated internally
    // ...
}

// After:
public async Task<Result> ExecuteAsync(Entity entity, Guid executionId, CancellationToken ct)
{
    // Use provided executionId
    // ...
}
```

**Impact**: All existing callers must provide ExecutionId.

### 7. Write Tests

**API Integration Test** (`test/{Service}.Integration.Tests/`):
```csharp
[Fact]
public async Task TriggerAction_ValidEntity_Returns202Accepted()
{
    // Arrange: Create entity via API
    var createResponse = await _client.PostAsync("/api/{entity}", ...);
    var entityId = /* extract from response */;
    
    // Act: Trigger action
    var response = await _client.PostAsync($"/api/{entity}/{entityId}/trigger", null);
    
    // Assert
    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    var result = await response.Content.ReadFromJsonAsync<Trigger{Action}Response>();
    Assert.NotNull(result);
    Assert.Equal(entityId, result.EntityId);
}
```

**Handler Unit Test** (`test/{Service}.Application.Tests/`):
```csharp
[Fact]
public async Task Handle_ManualTrigger_PublishesTriggeredEvent()
{
    // Arrange: Mock repository, service
    var command = new Execute{Action}
    {
        ClientId = "client123",
        EntityId = Guid.NewGuid(),
        IsManualTrigger = true,
        TriggeredBy = "user-456",
        // ... other fields
    };
    
    // Act: Handle command
    await _handler.Handle(command, _mockContext.Object);
    
    // Assert: Verify event published
    _mockContext.Verify(
        x => x.Publish(
            It.Is<{Action}Triggered>(e => 
                e.ClientId == "client123" &&
                e.IsManualTrigger == true &&
                e.TriggeredBy == "user-456"),
            It.IsAny<PublishOptions>()),
        Times.Once);
}
```

---

## Testing Scenarios

### Scenario 1: Happy Path (Manual Trigger)

**Setup**: Active configuration exists, user has access

**Steps**:
1. POST `/api/configuration/{id}/trigger` with valid JWT
2. API validates configuration
3. API sends `ExecuteFileCheck` command
4. API returns 202 Accepted
5. Handler receives command
6. Handler publishes `FileCheckTriggered` event
7. Handler executes file check
8. Handler publishes `FileCheckCompleted` event

**Assertions**:
- API returns 202 with tracking info
- Event published with `IsManualTrigger=true` and correct user ID
- File check executes normally

### Scenario 2: Security Trimming (Wrong Client)

**Setup**: Configuration exists but belongs to different client

**Steps**:
1. POST `/api/configuration/{id}/trigger` with JWT for ClientA
2. API queries configuration (belongs to ClientB)
3. Repository returns null (security trimming)
4. API returns 404 Not Found

**Assertions**:
- 404 response (no information disclosure)
- No command sent
- No event published

### Scenario 3: Inactive Configuration

**Setup**: Configuration exists but `IsActive=false`

**Steps**:
1. POST `/api/configuration/{id}/trigger`
2. API loads configuration
3. API checks `IsActive` field
4. API returns 400 Bad Request

**Assertions**:
- 400 response with clear error message
- No command sent
- No event published

### Scenario 4: Handler Idempotency

**Setup**: Handler processes command, fails, retries

**Steps**:
1. Handler generates ExecutionId (ABC)
2. Handler publishes `FileCheckTriggered` (Key=ABC)
3. Handler throws exception
4. NServiceBus retries handler
5. Handler generates SAME ExecutionId (ABC) - **Wait, this is wrong!**

**⚠️ Problem**: ExecutionId generation in handler creates new ID on each retry if using `Guid.NewGuid()` directly.

**Solution**: Store ExecutionId in handler's internal state or use message-derived stable ID.

**Corrected Idempotency Pattern**:
```csharp
// Option A: Use message properties to derive stable ExecutionId
var executionId = DeriveExecutionId(message); // Deterministic function

// Option B: Store in handler field (requires handler instance per message)
// Not recommended with NServiceBus (handlers are singletons)

// Option C: Generate once, store in database immediately
// Too complex for simple audit event

// ✅ RECOMMENDED: Accept that each handler invocation is a separate execution
// If handler retries, it's a new attempt → new ExecutionId → new event (by design)
```

**Final Decision from Research**: Generate `ExecutionId` once in handler. If handler retries, same invocation uses same ExecutionId. NServiceBus message deduplication ensures handler only processes message once per unique MessageId. Therefore, ExecutionId is stable per message processing attempt.

**Corrected Pattern**:
```csharp
public async Task Handle(Execute{Action} message, IMessageHandlerContext context)
{
    // ExecutionId stable for this message processing (not regenerated on retry)
    var executionId = Guid.NewGuid(); 
    
    // This looks like it regenerates, but NServiceBus handlers process each message once
    // Retries happen at message level (same message = same handler invocation context)
    
    await context.Publish(new {Action}Triggered
    {
        IdempotencyKey = $"{message.ClientId}:{message.EntityId}:triggered:{executionId}",
        ExecutionId = executionId,
        // ...
    });
    
    // Use same executionId for service call
    await _service.ExecuteAsync(entity, executionId, CancellationToken.None);
}
```

**Why This Works**: NServiceBus guarantees that each unique message is processed by handler exactly once. If handler throws exception, message is retried, but it's a **new message processing context** with potentially new ExecutionId. The idempotency is at the **message level** (MessageId), not handler code level.

**Better Approach for True Idempotency**: Use MessageId as ExecutionId:
```csharp
var executionId = message.MessageId; // Stable across retries!
```

But this couples execution tracking to message infrastructure. Trade-off decision documented in research.md (R7).

---

## Common Pitfalls

### ❌ Pitfall 1: Generating ExecutionId in API

**Problem**: API generates ExecutionId, includes in response, passes to command  
**Issue**: Multiple API retries create multiple executions with different IDs (confusing tracking)  
**Fix**: Generate ExecutionId in handler only

### ❌ Pitfall 2: Not Validating Before Sending Command

**Problem**: API sends command, handler discovers invalid entity  
**Issue**: Wastes message processing, confusing error flow, harder to return user-friendly errors  
**Fix**: Validate in API (fast-fail), validate again in handler (defense in depth)

### ❌ Pitfall 3: Publishing Event After Business Logic

**Problem**: Event published after service call completes  
**Issue**: If service fails, no event published (missing audit trail)  
**Fix**: Publish event BEFORE service call (captures intention, not outcome)

### ❌ Pitfall 4: Forgetting to Update Service Signature

**Problem**: Handler generates ExecutionId, service generates own ExecutionId  
**Issue**: ExecutionId in event doesn't match ExecutionId in execution record  
**Fix**: Pass ExecutionId to service as parameter

### ❌ Pitfall 5: Using Timestamp in IdempotencyKey

**Problem**: `IdempotencyKey = $"{ClientId}:{EntityId}:triggered:{DateTime.UtcNow.Ticks}"`  
**Issue**: Changes on every handler retry (not idempotent)  
**Fix**: Use stable ExecutionId in IdempotencyKey

---

## Security Patterns

### Pattern 1: Extract Claims from JWT

```csharp
private string GetClientIdFromClaims()
{
    var clientId = User.FindFirst("clientId")?.Value;
    if (string.IsNullOrWhiteSpace(clientId))
    {
        throw new UnauthorizedAccessException("ClientId claim is required");
    }
    return clientId;
}

private string GetUserIdFromClaims()
{
    return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst(ClaimTypes.Email)?.Value
        ?? "unknown-user";
}
```

**Usage**: Call at start of controller action, use in queries and audit logs.

### Pattern 2: Repository-Level Security Trimming

```csharp
// Repository automatically filters by clientId
public async Task<Entity> GetByIdAsync(string clientId, Guid entityId, CancellationToken ct)
{
    var response = await _container.ReadItemAsync<EntityDocument>(
        entityId.ToString(),
        new PartitionKey(clientId), // ← Security trimming via partition key
        cancellationToken: ct);
    
    return MapToEntity(response.Resource);
}
```

**Effect**: Cross-client queries return null (DocumentNotFoundException), not unauthorized error.

### Pattern 3: 404 for Authorization Failures

**Don't reveal what user doesn't own**:
- ✅ Return 404 for "not found" AND "wrong client"
- ❌ Return 403 "Forbidden" (reveals existence)

---

## Performance Optimization

### Caching Considerations

**Don't cache**: Entity state must be current (IsActive check)  
**Do cache**: Configuration schema, protocol adapters (static data)

### Async All the Way

```csharp
// ✅ Good: Fully asynchronous
public async Task<IActionResult> TriggerAction(...)
{
    var entity = await _service.GetByIdAsync(...);
    await _messageSession.Send(...);
    return Accepted(...);
}

// ❌ Bad: Blocking call
public async Task<IActionResult> TriggerAction(...)
{
    var entity = _service.GetByIdAsync(...).Result; // Deadlock risk!
    // ...
}
```

### Message Sending Performance

**Azure Service Bus**:
- `Send()`: ~10-50ms typical
- Batching: Not needed for single trigger operations
- Connection pooling: Handled by NServiceBus

---

## Monitoring & Observability

### Structured Logging Template

```csharp
_logger.LogInformation(
    "Triggering {Action} for {Entity} {EntityId} (Client: {ClientId}, User: {UserId}, CorrelationId: {CorrelationId})",
    "{action}",
    "{entity}",
    entityId,
    clientId,
    userId,
    correlationId);
```

**Required Fields**:
- Entity identifiers (EntityId, ClientId)
- User identifier (for audit)
- Correlation ID (for tracing)

### Application Insights Custom Events

```csharp
_telemetryClient.TrackEvent("{Action}Triggered", new Dictionary<string, string>
{
    ["ClientId"] = clientId,
    ["EntityId"] = entityId.ToString(),
    ["IsManual"] = isManualTrigger.ToString(),
    ["TriggeredBy"] = userId
});
```

### Dashboard Queries (KQL)

**Manual trigger frequency**:
```kusto
customEvents
| where name == "{Action}Triggered"
| where customDimensions.IsManualTrigger == "true"
| summarize count() by bin(timestamp, 1h)
```

**Top triggering users**:
```kusto
customEvents
| where name == "{Action}Triggered"
| where customDimensions.IsManualTrigger == "true"
| summarize TriggerCount = count() by tostring(customDimensions.TriggeredBy)
| order by TriggerCount desc
| take 10
```

---

## Trade-offs & Decisions

### Decision 1: ExecutionId in Response or Not?

**Option A**: Return ExecutionId in API response
- ✅ Pro: Client can immediately track execution
- ❌ Con: Requires generating ExecutionId in API (breaks idempotency pattern)
- ❌ Con: Couples API response to internal execution tracking

**Option B**: Don't return ExecutionId
- ✅ Pro: Simpler API (fewer fields)
- ✅ Pro: Handler generates ExecutionId (better idempotency)
- ❌ Con: Client must query execution history to find ExecutionId

**This Guide's Choice**: Option B (simpler, better idempotency)  
**FileRetrieval Implementation**: Option A (UX priority, see research.md R7 for idempotency strategy)

### Decision 2: Validate in API or Handler?

**Best Practice**: Validate in BOTH (defense in depth)

**API Validation** (fast-fail):
- Entity exists
- User has access
- Entity state allows operation
- Return user-friendly errors

**Handler Validation** (safety):
- Entity still exists (could be deleted between API call and handler)
- State hasn't changed (could be deactivated)
- Skip processing if invalid

### Decision 3: Event Timing

**Option A**: Publish after execution completes
- ❌ Missing audit trail if execution fails mid-processing

**Option B**: Publish before execution starts
- ✅ Captures all trigger attempts
- ✅ Monitors queued executions
- ✅ Complete audit trail

**Choice**: Option B (before execution)

---

## Reusable Code Snippets

### Security Helper Methods

```csharp
// In BaseController or shared class
protected string GetClientIdFromClaims()
{
    var clientId = User.FindFirst("clientId")?.Value;
    if (string.IsNullOrWhiteSpace(clientId))
    {
        throw new UnauthorizedAccessException("ClientId claim is required but not found in token");
    }
    return clientId;
}

protected string GetUserIdFromClaims()
{
    return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst(ClaimTypes.Email)?.Value
        ?? "unknown-user";
}
```

### Correlation ID Generator

```csharp
private static string GenerateCorrelationId(string clientId, Guid entityId)
{
    return $"manual-trigger-{clientId}-{entityId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
}
```

### Event Publishing Pattern

```csharp
await context.Publish(new {Action}Triggered
{
    MessageId = Guid.NewGuid(),
    CorrelationId = message.CorrelationId,
    OccurredUtc = DateTimeOffset.UtcNow,
    IdempotencyKey = $"{message.ClientId}:{message.EntityId}:triggered:{executionId}",
    ClientId = message.ClientId,
    EntityId = message.EntityId,
    EntityName = entity.Name,
    ExecutionId = executionId,
    IsManualTrigger = message.IsManualTrigger,
    TriggeredBy = message.TriggeredBy ?? "Scheduler"
});
```

---

## Next Steps

After implementing this pattern:

1. **Document in Domain Standards**: Add manual trigger pattern to `docs/{domain}-standards.md`
2. **Update OpenAPI/Swagger**: Document trigger endpoint with examples
3. **Add Monitoring**: Set up Application Insights dashboard for trigger metrics
4. **Train Team**: Share this quickstart with team for future features
5. **Consider Abstraction**: If multiple entities need triggers, consider base controller or shared service

---

## Summary

**Key Takeaways**:
- ✅ Validate in API, send command, return 202 Accepted immediately
- ✅ Handler publishes event BEFORE executing business logic
- ✅ Generate ExecutionId in handler for idempotency
- ✅ Use existing security patterns (JWT claims + repository filtering)
- ✅ Test at both API and handler layers
- ✅ Follow 202 Accepted pattern (async operations)

**Files to Create/Modify**:
1. Event contract (new)
2. Response DTO (new)
3. API endpoint (add to existing controller)
4. Handler modification (publish event)
5. Service signature update (accept executionId)
6. Tests (integration + unit)

**Estimated Effort**: 4-6 hours for experienced developer (including tests)

---

**Document Status**: ✅ Complete - Ready for implementation
