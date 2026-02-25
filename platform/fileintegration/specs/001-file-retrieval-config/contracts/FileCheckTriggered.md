# Contract Specification: FileCheckTriggered Event

**Version**: 1.0  
**Created**: 2025-01-24  
**Type**: Event (Past-tense notification)  
**Delivery**: At-least-once with outbox deduplication  
**Scope**: FileRetrieval bounded context (internal event, may become cross-service if monitoring services subscribe)

---

## Purpose

Notifies subscribers that a file check has been initiated for a specific FileRetrievalConfiguration. Captures trigger context (manual vs scheduled), user identity, and execution tracking information for audit trail, monitoring, and analytics.

**Published When**: 
- Immediately after `ExecuteFileCheckHandler` loads and validates configuration
- Before actual file check execution begins
- Triggered by both scheduled executions (SchedulerHostedService) and manual API triggers

**Business Value**:
- Enables audit trail of who triggered which file checks and when
- Distinguishes manual troubleshooting from automated scheduled checks
- Provides monitoring visibility into system activity before execution completes
- Supports analytics on trigger patterns and support engineer activity

---

## Message Contract

### C# Record Definition

**Location**: `src/FileRetrieval.Contracts/Events/FileCheckTriggered.cs`

```csharp
using NServiceBus;

namespace FileRetrieval.Contracts.Events;

/// <summary>
/// Event published when a file check is triggered (before execution begins).
/// Captures trigger source (scheduled vs manual) and user context for audit trail.
/// Published by ExecuteFileCheckHandler before calling FileCheckService.
/// </summary>
public record FileCheckTriggered : IEvent
{
    // Standard message metadata
    public Guid MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset OccurredUtc { get; init; }
    public required string IdempotencyKey { get; init; }
    
    // File Retrieval context
    public required string ClientId { get; init; }
    public required Guid ConfigurationId { get; init; }
    public required string ConfigurationName { get; init; }
    public required string Protocol { get; init; }
    
    // Execution tracking
    public required Guid ExecutionId { get; init; }
    public required DateTimeOffset ScheduledExecutionTime { get; init; }
    
    // Trigger context
    public required bool IsManualTrigger { get; init; }
    public required string TriggeredBy { get; init; }
}
```

---

## Field Specifications

### Standard Message Metadata

| Field | Type | Required | Purpose | Source |
|-------|------|----------|---------|--------|
| `MessageId` | Guid | Yes | Unique event identifier for deduplication | `Guid.NewGuid()` |
| `CorrelationId` | string | Yes | Trace ID across distributed operations | From `ExecuteFileCheck` command |
| `OccurredUtc` | DateTimeOffset | Yes | When event was published | `DateTimeOffset.UtcNow` |
| `IdempotencyKey` | string | Yes | Deduplication key for outbox | `"{ClientId}:{ConfigurationId}:triggered:{ExecutionId}"` |

### File Retrieval Context

| Field | Type | Required | Purpose | Source |
|-------|------|----------|---------|--------|
| `ClientId` | string | Yes | Client identifier (partition key) | From `ExecuteFileCheck` command |
| `ConfigurationId` | Guid | Yes | Configuration being checked | From `ExecuteFileCheck` command |
| `ConfigurationName` | string | Yes | Human-readable configuration name | From loaded `FileRetrievalConfiguration` entity |
| `Protocol` | string | Yes | Protocol type (FTP, HTTPS, AzureBlob) | From `configuration.ProtocolSettings.ProtocolType.ToString()` |

### Execution Tracking

| Field | Type | Required | Purpose | Source |
|-------|------|----------|---------|--------|
| `ExecutionId` | Guid | Yes | Unique identifier for this file check execution | Generated in handler before publishing event |
| `ScheduledExecutionTime` | DateTimeOffset | Yes | When check was scheduled to run | From command (scheduled) or `DateTimeOffset.UtcNow` (manual) |

### Trigger Context

| Field | Type | Required | Purpose | Source |
|-------|------|----------|---------|--------|
| `IsManualTrigger` | bool | Yes | Distinguish manual (true) from scheduled (false) | From `ExecuteFileCheck.IsManualTrigger` |
| `TriggeredBy` | string | Yes | User ID (manual) or "Scheduler" (scheduled) | From `ExecuteFileCheck.TriggeredBy ?? "Scheduler"` |

---

## Idempotency Specification

### IdempotencyKey Format

**Pattern**: `"{ClientId}:{ConfigurationId}:triggered:{ExecutionId}"`

**Example**: `"client123:456def:triggered:789abc"`

**Uniqueness Guarantee**:
- `ClientId` + `ConfigurationId` = which configuration
- `ExecutionId` = which specific execution attempt
- Keyword "triggered" = distinguishes from "completed"/"failed" events

### Deduplication Behavior

**Scenario 1: Normal Flow (No Retries)**
```text
Handler invocation 1:
  - Generate ExecutionId = ABC
  - Publish FileCheckTriggered (Key = "client:config:triggered:ABC")
  - Outbox stores event
  - Event delivered to subscribers ✓
```

**Scenario 2: Handler Retries After Failure**
```text
Handler invocation 1:
  - Generate ExecutionId = ABC
  - Publish FileCheckTriggered (Key = "client:config:triggered:ABC")
  - Outbox stores event
  - Handler throws exception (e.g., service unavailable)
  
Handler invocation 2 (retry):
  - Use SAME ExecutionId = ABC (stable across retries)
  - Publish FileCheckTriggered (Key = "client:config:triggered:ABC")
  - Outbox detects duplicate key → Skip publishing ✓
  - Continue processing
  - Event delivered to subscribers exactly once ✓
```

**Scenario 3: Multiple API Requests**
```text
API Request 1: POST /trigger → Handler with ExecutionId = ABC → Event published ✓
API Request 2: POST /trigger → Handler with ExecutionId = XYZ → Event published ✓
                                        ↑ Different execution, different event (by design)
```

### Implementation Pattern

**In Handler** (pseudocode):
```csharp
// Generate stable ExecutionId for this handler invocation
var executionId = Guid.NewGuid(); // ← Generated ONCE per handler invocation, not in service

// Publish event with idempotency key
await context.Publish(new FileCheckTriggered
{
    IdempotencyKey = $"{message.ClientId}:{message.ConfigurationId}:triggered:{executionId}",
    ExecutionId = executionId,
    // ... other fields
});

// Pass same executionId to service
await _fileCheckService.ExecuteCheckAsync(configuration, scheduledTime, executionId, ct);
```

---

## Schema Evolution

### Version 1.0 (Initial Release)

All fields defined above. No optional fields except message metadata.

### Future Extensibility

**Potential Future Fields** (not in v1.0):
- `TriggerSource` (string): "API", "Scheduler", "Webhook", "CLI" (if more trigger sources added)
- `RequestMetadata` (object): IP address, user agent, request ID (if detailed audit required)
- `Priority` (int): Priority level for execution queue (if priority queuing added)

**Compatibility Strategy**:
- Add new fields as nullable/optional
- Never remove or rename existing fields
- Use new event types for breaking changes (e.g., `FileCheckTriggeredV2`)

---

## Subscriber Guidelines

### Expected Subscribers

**Audit Logging System**:
- Captures all FileCheckTriggered events
- Stores for compliance and security audits
- Retention: 1+ years

**Monitoring Dashboard**:
- Displays recent manual triggers
- Shows trigger frequency by user
- Alerts on unusual trigger patterns

**Analytics System**:
- Calculates manual vs scheduled ratio
- Identifies most-triggered configurations
- Support engineer activity metrics

### Subscription Pattern

**NServiceBus Configuration** (in subscriber endpoint):
```csharp
endpointConfiguration.ConfigureRouting()
    .SubscribeTo<FileCheckTriggered>("FileRetrieval.Worker");
```

**Handler Example**:
```csharp
public class FileCheckTriggeredHandler : IHandleMessages<FileCheckTriggered>
{
    public async Task Handle(FileCheckTriggered message, IMessageHandlerContext context)
    {
        // Process event
        _logger.LogInformation(
            "File check triggered: {ConfigurationId} by {User} ({TriggerType})",
            message.ConfigurationId,
            message.TriggeredBy,
            message.IsManualTrigger ? "Manual" : "Scheduled");
        
        // Store in audit log, update dashboard, etc.
    }
}
```

### Error Handling for Subscribers

**Principle**: Event publishing succeeds even if subscribers fail (eventual consistency).

**Handler Requirements**:
- Must be idempotent (may receive same event multiple times)
- Must handle replay (use IdempotencyKey for deduplication)
- Must not throw exceptions for non-critical failures (e.g., notification delivery)
- Critical subscribers should retry on failure (NServiceBus automatic retries)

---

## Observability

### Logging Requirements

**When Publishing Event** (in handler):
```csharp
_logger.LogInformation(
    "Publishing FileCheckTriggered event for configuration {ConfigurationId} (Client: {ClientId}, ExecutionId: {ExecutionId}, TriggeredBy: {TriggeredBy}, Manual: {IsManual})",
    message.ConfigurationId,
    message.ClientId,
    executionId,
    message.TriggeredBy ?? "Scheduler",
    message.IsManualTrigger);
```

**Structured Log Fields**:
- `ConfigurationId`: Configuration being checked
- `ClientId`: Client identifier (partition key)
- `ExecutionId`: Execution tracking ID
- `CorrelationId`: Request correlation ID
- `TriggeredBy`: User or "Scheduler"
- `IsManual`: Boolean flag

### Metrics to Track

**Custom Metrics** (Application Insights):
- `FileRetrieval.FileCheckTriggered.Count` (dimension: `IsManualTrigger`, `Protocol`)
- `FileRetrieval.ManualTrigger.ByUser.Count` (dimension: `TriggeredBy`)
- `FileRetrieval.TriggerToCompletion.Duration` (track ExecutionId from triggered → completed)

**Dashboard Queries** (KQL examples):
```kusto
// Manual trigger frequency by day
customEvents
| where name == "FileCheckTriggered"
| where customDimensions.IsManualTrigger == "true"
| summarize count() by bin(timestamp, 1d)

// Top support engineers by manual triggers
customEvents
| where name == "FileCheckTriggered"
| where customDimensions.IsManualTrigger == "true"
| summarize TriggerCount = count() by tostring(customDimensions.TriggeredBy)
| order by TriggerCount desc
```

---

## Contract Summary

**Event**: `FileCheckTriggered`  
**Namespace**: `FileRetrieval.Contracts.Events`  
**Version**: 1.0  
**Publisher**: `ExecuteFileCheckHandler` (FileRetrieval.Worker)  
**Subscribers**: Audit systems, monitoring dashboards, analytics (to be implemented)  
**Frequency**: Low (~100-200/day, mostly scheduled, ~10-20 manual)  
**Size**: ~500 bytes per event  
**Retention**: Per subscriber (recommended 30-90 days)

**Idempotency Key**: `"{ClientId}:{ConfigurationId}:triggered:{ExecutionId}"`  
**Uniqueness**: Per execution (same ExecutionId across handler retries)  
**Deduplication**: NServiceBus outbox ensures exactly-once delivery

---

**Document Status**: ✅ Complete - Ready for implementation
