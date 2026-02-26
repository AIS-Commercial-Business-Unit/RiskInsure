# Event Contracts: Client File Retrieval Configuration

**Date**: 2025-01-24  
**Feature**: 001-file-retrieval-config  
**Phase**: Phase 1 - Design

## Overview

This document defines all event message contracts for the Client File Retrieval Configuration feature. Events are past-tense notifications broadcast to all interested subscribers via NServiceBus. All events follow the RiskInsure naming convention: `Noun + VerbPastTense` (e.g., `FileDiscovered`, `ConfigurationCreated`).

Events integrate with the workflow orchestration platform and other subscribers via Azure Service Bus publish/subscribe pattern.

---

## Message Standards

All events MUST include:
- `MessageId` (Guid) - Unique message identifier (NServiceBus auto-generated)
- `CorrelationId` (string) - Trace messages across distributed calls
- `OccurredUtc` (DateTimeOffset) - When event occurred
- `IdempotencyKey` (string) - Unique key for duplicate detection
- `ClientId` (string) - Multi-tenant isolation

---

## 1. FileDiscovered

**Purpose**: Notifies that a file matching a FileRetrievalConfiguration has been found.

**Published By**: `FileCheckService` (when file is discovered)  
**Subscribers**: Workflow orchestration platform, monitoring systems, audit logs

**Properties**:

```csharp
public record FileDiscovered : IEvent
{
    public Guid MessageId { get; init; }
    public string CorrelationId { get; init; } = default!;
    public DateTimeOffset OccurredUtc { get; init; }
    public string IdempotencyKey { get; init; } = default!;
    
    // File details
    public string ClientId { get; init; } = default!;
    public Guid ConfigurationId { get; init; }
    public Guid ExecutionId { get; init; }
    public Guid DiscoveredFileId { get; init; }
    public string FileUrl { get; init; } = default!;
    public string Filename { get; init; } = default!;
    public long? FileSize { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public DateTimeOffset DiscoveredAt { get; init; }
    
    // Configuration metadata
    public string ConfigurationName { get; init; } = default!;
    public string Protocol { get; init; } = default!; // "Ftp", "Https", "AzureBlob"
    
    // Custom event data from EventDefinition
    public Dictionary<string, object>? EventData { get; init; }
}
```

**Idempotency**:
- `IdempotencyKey`: `{ClientId}:{DiscoveredFileId}`
- Subscribers must handle duplicate events gracefully (check if already processed)

**Subscriber Responsibilities**:
- **Workflow Platform**: Start workflow instance to process file
- **Monitoring System**: Log file discovery for dashboard/reporting
- **Audit Log**: Record file discovery event for compliance

**Example Event Data** (from EventDefinition):
```json
{
  "fileType": "Transaction",
  "priority": "High",
  "department": "Finance",
  "expectedRecordCount": 1000
}
```

**Usage**:
```csharp
await context.Publish(new FileDiscovered
{
    MessageId = Guid.NewGuid(),
    CorrelationId = $"{clientId}-{configurationId}-{executionId}",
    OccurredUtc = DateTimeOffset.UtcNow,
    IdempotencyKey = $"{clientId}:{discoveredFileId}",
    ClientId = clientId,
    ConfigurationId = configurationId,
    ExecutionId = executionId,
    DiscoveredFileId = discoveredFileId,
    FileUrl = "ftp://ftp.client.com/files/2025/01/trans_20250124.csv",
    Filename = "trans_20250124.csv",
    FileSize = 524288,
    LastModified = DateTimeOffset.Parse("2025-01-24T06:00:00Z"),
    DiscoveredAt = DateTimeOffset.UtcNow,
    ConfigurationName = "Daily Transaction Files",
    Protocol = "Ftp",
    EventData = new Dictionary<string, object>
    {
        ["fileType"] = "Transaction",
        ["priority"] = "High"
    }
});
```

---

## 2. FileCheckCompleted

**Purpose**: Notifies that a scheduled file check has completed successfully.

**Published By**: `ExecuteFileCheckHandler` (after FileCheckService completes)  
**Subscribers**: Monitoring systems, dashboard, operational alerts

**Properties**:

```csharp
public record FileCheckCompleted : IEvent
{
    public Guid MessageId { get; init; }
    public string CorrelationId { get; init; } = default!;
    public DateTimeOffset OccurredUtc { get; init; }
    public string IdempotencyKey { get; init; } = default!;
    
    // Execution details
    public string ClientId { get; init; } = default!;
    public Guid ConfigurationId { get; init; }
    public Guid ExecutionId { get; init; }
    public string ConfigurationName { get; init; } = default!;
    public string Protocol { get; init; } = default!;
    
    // Execution results
    public DateTimeOffset ExecutionStartedAt { get; init; }
    public DateTimeOffset ExecutionCompletedAt { get; init; }
    public int FilesFound { get; init; }
    public int FilesProcessed { get; init; }
    public long DurationMs { get; init; }
    
    // Resolved patterns (after token replacement)
    public string ResolvedFilePathPattern { get; init; } = default!;
    public string ResolvedFilenamePattern { get; init; } = default!;
}
```

**Idempotency**:
- `IdempotencyKey`: `{ClientId}:{ExecutionId}`

**Subscriber Responsibilities**:
- **Monitoring System**: Update dashboard with execution success
- **Metrics Service**: Track success rate, duration, files discovered
- **Operational Alerts**: Suppress alerts if check completes successfully

**Usage**:
```csharp
await context.Publish(new FileCheckCompleted
{
    MessageId = Guid.NewGuid(),
    CorrelationId = correlationId,
    OccurredUtc = DateTimeOffset.UtcNow,
    IdempotencyKey = $"{clientId}:{executionId}",
    ClientId = clientId,
    ConfigurationId = configurationId,
    ExecutionId = executionId,
    ConfigurationName = "Daily Transaction Files",
    Protocol = "Ftp",
    ExecutionStartedAt = executionStartedAt,
    ExecutionCompletedAt = DateTimeOffset.UtcNow,
    FilesFound = 1,
    FilesProcessed = 1,
    DurationMs = 5234,
    ResolvedFilePathPattern = "/transactions/2025/01",
    ResolvedFilenamePattern = "trans_20250124.csv"
});
```

---

## 3. FileCheckFailed

**Purpose**: Notifies that a scheduled file check has failed after retry attempts.

**Published By**: `ExecuteFileCheckHandler` (after FileCheckService fails)  
**Subscribers**: Monitoring systems, operational alerts, incident management

**Properties**:

```csharp
public record FileCheckFailed : IEvent
{
    public Guid MessageId { get; init; }
    public string CorrelationId { get; init; } = default!;
    public DateTimeOffset OccurredUtc { get; init; }
    public string IdempotencyKey { get; init; } = default!;
    
    // Execution details
    public string ClientId { get; init; } = default!;
    public Guid ConfigurationId { get; init; }
    public Guid ExecutionId { get; init; }
    public string ConfigurationName { get; init; } = default!;
    public string Protocol { get; init; } = default!;
    
    // Execution results
    public DateTimeOffset ExecutionStartedAt { get; init; }
    public DateTimeOffset ExecutionFailedAt { get; init; }
    public long DurationMs { get; init; }
    public int RetryCount { get; init; }
    
    // Error details
    public string ErrorMessage { get; init; } = default!;
    public string ErrorCategory { get; init; } = default!; // "AuthenticationFailure", "ConnectionTimeout", etc.
    public string? StackTrace { get; init; } // Optional, for debugging
    
    // Resolved patterns (after token replacement)
    public string ResolvedFilePathPattern { get; init; } = default!;
    public string ResolvedFilenamePattern { get; init; } = default!;
}
```

**Idempotency**:
- `IdempotencyKey`: `{ClientId}:{ExecutionId}`

**Error Categories** (from research.md):
- `AuthenticationFailure` - Invalid credentials, access denied
- `ConnectionTimeout` - Network timeout, server unreachable
- `FileNotFound` - No files matching pattern (may be normal)
- `InvalidConfiguration` - Configuration error (tokens, protocol settings)
- `ProtocolError` - FTP/HTTPS/Azure Blob error
- `TokenReplacementError` - Invalid token usage

**Subscriber Responsibilities**:
- **Monitoring System**: Update dashboard with failure
- **Operational Alerts**: Send alert to operations team (email, Slack, PagerDuty)
- **Incident Management**: Create incident ticket for investigation
- **Metrics Service**: Track failure rate by error category

**Usage**:
```csharp
await context.Publish(new FileCheckFailed
{
    MessageId = Guid.NewGuid(),
    CorrelationId = correlationId,
    OccurredUtc = DateTimeOffset.UtcNow,
    IdempotencyKey = $"{clientId}:{executionId}",
    ClientId = clientId,
    ConfigurationId = configurationId,
    ExecutionId = executionId,
    ConfigurationName = "Daily Transaction Files",
    Protocol = "Ftp",
    ExecutionStartedAt = executionStartedAt,
    ExecutionFailedAt = DateTimeOffset.UtcNow,
    DurationMs = 30000,
    RetryCount = 3,
    ErrorMessage = "Connection timeout after 30 seconds",
    ErrorCategory = "ConnectionTimeout",
    StackTrace = null, // Omit in production for security
    ResolvedFilePathPattern = "/transactions/2025/01",
    ResolvedFilenamePattern = "trans_20250124.csv"
});
```

---

## 4. ConfigurationCreated

**Purpose**: Notifies that a new FileRetrievalConfiguration has been created.

**Published By**: `CreateConfigurationHandler` (after successful creation)  
**Subscribers**: Audit log, monitoring systems, scheduler (to refresh configuration cache)

**Properties**:

```csharp
public record ConfigurationCreated : IEvent
{
    public Guid MessageId { get; init; }
    public string CorrelationId { get; init; } = default!;
    public DateTimeOffset OccurredUtc { get; init; }
    public string IdempotencyKey { get; init; } = default!;
    
    // Configuration details
    public string ClientId { get; init; } = default!;
    public Guid ConfigurationId { get; init; }
    public string Name { get; init; } = default!;
    public string Protocol { get; init; } = default!;
    public string FilePathPattern { get; init; } = default!;
    public string FilenamePattern { get; init; } = default!;
    public string CronExpression { get; init; } = default!;
    public string Timezone { get; init; } = default!;
    public bool IsActive { get; init; }
    public string CreatedBy { get; init; } = default!;
    
    // Metadata
    public int EventCount { get; init; } // Number of events to publish
    public int CommandCount { get; init; } // Number of commands to send
}
```

**Idempotency**:
- `IdempotencyKey`: `{ClientId}:{ConfigurationId}`

**Subscriber Responsibilities**:
- **Audit Log**: Record configuration creation for compliance
- **Scheduler**: Refresh configuration cache (load new configuration for scheduling)
- **Monitoring Dashboard**: Update configuration count metrics

**Usage**:
```csharp
await context.Publish(new ConfigurationCreated
{
    MessageId = Guid.NewGuid(),
    CorrelationId = correlationId,
    OccurredUtc = DateTimeOffset.UtcNow,
    IdempotencyKey = $"{clientId}:{configurationId}",
    ClientId = clientId,
    ConfigurationId = configurationId,
    Name = "Daily Transaction Files",
    Protocol = "Ftp",
    FilePathPattern = "/transactions/{yyyy}/{mm}",
    FilenamePattern = "trans_{yyyy}{mm}{dd}.csv",
    CronExpression = "0 8 * * *",
    Timezone = "America/New_York",
    IsActive = true,
    CreatedBy = "admin@riskinsure.com",
    EventCount = 1,
    CommandCount = 1
});
```

---

## 5. ConfigurationUpdated

**Purpose**: Notifies that a FileRetrievalConfiguration has been updated.

**Published By**: `UpdateConfigurationHandler` (after successful update)  
**Subscribers**: Audit log, monitoring systems, scheduler (to refresh configuration cache)

**Properties**:

```csharp
public record ConfigurationUpdated : IEvent
{
    public Guid MessageId { get; init; }
    public string CorrelationId { get; init; } = default!;
    public DateTimeOffset OccurredUtc { get; init; }
    public string IdempotencyKey { get; init; } = default!;
    
    // Configuration details
    public string ClientId { get; init; } = default!;
    public Guid ConfigurationId { get; init; }
    public string Name { get; init; } = default!;
    public string Protocol { get; init; } = default!;
    public string FilePathPattern { get; init; } = default!;
    public string FilenamePattern { get; init; } = default!;
    public string CronExpression { get; init; } = default!;
    public string Timezone { get; init; } = default!;
    public bool IsActive { get; init; }
    public string LastModifiedBy { get; init; } = default!;
    
    // Change tracking
    public List<string> ChangedFields { get; init; } = default!; // List of field names that changed
}
```

**Idempotency**:
- `IdempotencyKey`: `{ClientId}:{ConfigurationId}:{OccurredUtc:yyyyMMddHHmmss}`

**Subscriber Responsibilities**:
- **Audit Log**: Record configuration changes for compliance
- **Scheduler**: Refresh configuration cache (reload updated configuration)
- **Monitoring Dashboard**: Update metrics if schedule or protocol changed

**ChangedFields Examples**:
- `["Name", "Schedule"]` - Name and schedule changed
- `["IsActive"]` - Configuration activated/deactivated
- `["ProtocolSettings"]` - Connection settings updated

**Usage**:
```csharp
await context.Publish(new ConfigurationUpdated
{
    MessageId = Guid.NewGuid(),
    CorrelationId = correlationId,
    OccurredUtc = DateTimeOffset.UtcNow,
    IdempotencyKey = $"{clientId}:{configurationId}:{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
    ClientId = clientId,
    ConfigurationId = configurationId,
    Name = "Daily Transaction Files",
    Protocol = "Ftp",
    FilePathPattern = "/transactions/{yyyy}/{mm}",
    FilenamePattern = "trans_{yyyy}{mm}{dd}.csv",
    CronExpression = "0 9 * * *", // Changed from 8 AM to 9 AM
    Timezone = "America/New_York",
    IsActive = true,
    LastModifiedBy = "admin@riskinsure.com",
    ChangedFields = new List<string> { "CronExpression" }
});
```

---

## 6. ConfigurationDeleted

**Purpose**: Notifies that a FileRetrievalConfiguration has been deleted (soft-deleted).

**Published By**: `DeleteConfigurationHandler` (after successful deletion)  
**Subscribers**: Audit log, monitoring systems, scheduler (to remove from cache)

**Properties**:

```csharp
public record ConfigurationDeleted : IEvent
{
    public Guid MessageId { get; init; }
    public string CorrelationId { get; init; } = default!;
    public DateTimeOffset OccurredUtc { get; init; }
    public string IdempotencyKey { get; init; } = default!;
    
    // Configuration details
    public string ClientId { get; init; } = default!;
    public Guid ConfigurationId { get; init; }
    public string Name { get; init; } = default!;
    public string DeletedBy { get; init; } = default!;
    
    // Deletion metadata
    public bool IsSoftDelete { get; init; } // Always true for now (IsActive = false)
}
```

**Idempotency**:
- `IdempotencyKey`: `{ClientId}:{ConfigurationId}:{DeletedBy}:{OccurredUtc:yyyyMMddHHmmss}`

**Subscriber Responsibilities**:
- **Audit Log**: Record configuration deletion for compliance
- **Scheduler**: Remove configuration from cache (stop scheduling checks)
- **Monitoring Dashboard**: Update configuration count metrics

**Usage**:
```csharp
await context.Publish(new ConfigurationDeleted
{
    MessageId = Guid.NewGuid(),
    CorrelationId = correlationId,
    OccurredUtc = DateTimeOffset.UtcNow,
    IdempotencyKey = $"{clientId}:{configurationId}:{deletedBy}:{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
    ClientId = clientId,
    ConfigurationId = configurationId,
    Name = "Daily Transaction Files",
    DeletedBy = "admin@riskinsure.com",
    IsSoftDelete = true
});
```

---

## Event Flow Examples

### Example 1: Successful File Check with Discovery

```
ExecuteFileCheck command received
  ↓
FileCheckService.ExecuteCheck()
  ↓
FtpProtocolAdapter finds 2 files
  ↓
For each file:
  Create DiscoveredFile record
  Publish: FileDiscovered event (2 events total)
  ↓
Publish: FileCheckCompleted event
  ↓
Subscribers:
  - Workflow Platform receives FileDiscovered (2 events) → Starts 2 workflow instances
  - Monitoring System receives FileCheckCompleted → Updates dashboard
  - Audit Log receives FileDiscovered + FileCheckCompleted → Logs events
```

### Example 2: Failed File Check (Connection Timeout)

```
ExecuteFileCheck command received
  ↓
FileCheckService.ExecuteCheck()
  ↓
FtpProtocolAdapter connection timeout (retry 3x)
  ↓
After 3 retries: Failure
  ↓
Publish: FileCheckFailed event
  ↓
Subscribers:
  - Operational Alerts receives FileCheckFailed → Sends alert to ops team
  - Monitoring System receives FileCheckFailed → Updates dashboard with error
  - Incident Management receives FileCheckFailed → Creates incident ticket
```

### Example 3: Configuration Lifecycle

```
API: POST /api/configurations
  ↓
CreateConfiguration command sent
  ↓
CreateConfigurationHandler saves to Cosmos DB
  ↓
Publish: ConfigurationCreated event
  ↓
Subscribers:
  - Scheduler receives ConfigurationCreated → Loads configuration into cache
  - Audit Log receives ConfigurationCreated → Logs creation event
  
Later: API: PUT /api/configurations/{id}
  ↓
UpdateConfiguration command sent
  ↓
UpdateConfigurationHandler updates Cosmos DB
  ↓
Publish: ConfigurationUpdated event
  ↓
Subscribers:
  - Scheduler receives ConfigurationUpdated → Refreshes configuration in cache
  - Audit Log receives ConfigurationUpdated → Logs change with ChangedFields
  
Later: API: DELETE /api/configurations/{id}
  ↓
DeleteConfiguration command sent
  ↓
DeleteConfigurationHandler sets IsActive = false
  ↓
Publish: ConfigurationDeleted event
  ↓
Subscribers:
  - Scheduler receives ConfigurationDeleted → Removes configuration from cache
  - Audit Log receives ConfigurationDeleted → Logs deletion event
```

---

## NServiceBus Subscription Configuration

### Workflow Orchestration Platform (Subscriber)

```csharp
// Subscribe to file discovery events
endpointConfiguration.Conventions()
    .DefiningEventsAs(type => type.Namespace?.StartsWith("FileRetrieval.Contracts.Events") == true);

// Event handlers
public class FileDiscoveredHandler : IHandleMessages<FileDiscovered>
{
    public async Task Handle(FileDiscovered message, IMessageHandlerContext context)
    {
        // Start workflow instance to process file
        await _workflowService.StartWorkflowAsync(message.FileUrl, message.EventData);
    }
}
```

### Monitoring System (Subscriber)

```csharp
// Subscribe to all file retrieval events
public class FileCheckCompletedHandler : IHandleMessages<FileCheckCompleted>
{
    public async Task Handle(FileCheckCompleted message, IMessageHandlerContext context)
    {
        // Update dashboard metrics
        await _metricsService.RecordSuccessfulCheck(message.ClientId, message.ConfigurationId, message.DurationMs);
    }
}

public class FileCheckFailedHandler : IHandleMessages<FileCheckFailed>
{
    public async Task Handle(FileCheckFailed message, IMessageHandlerContext context)
    {
        // Update dashboard metrics + send alert
        await _metricsService.RecordFailedCheck(message.ClientId, message.ConfigurationId, message.ErrorCategory);
        await _alertService.SendAlert(message.ErrorCategory, message.ErrorMessage);
    }
}
```

---

## Summary

This contracts document defines:

✅ **6 event types**: FileDiscovered, FileCheckCompleted, FileCheckFailed, ConfigurationCreated, ConfigurationUpdated, ConfigurationDeleted  
✅ **Past-tense naming**: All events follow `Noun + VerbPastTense` convention  
✅ **Publish/Subscribe pattern**: Events broadcast to all interested subscribers  
✅ **Idempotency support**: All events include IdempotencyKey for duplicate detection  
✅ **Multi-tenant isolation**: All events include ClientId for security filtering  
✅ **Rich metadata**: Events include all necessary context for subscribers  
✅ **Cross-platform integration**: Events trigger workflows, monitoring, alerts, audit logs  
✅ **Error handling**: FileCheckFailed event provides detailed error information for troubleshooting  

Ready to proceed to **quickstart.md** (developer onboarding guide).
