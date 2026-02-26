# Data Model: Client File Retrieval Configuration

**Date**: 2025-01-24  
**Feature**: 001-file-retrieval-config  
**Phase**: Phase 1 - Design

## Overview

This document defines the data model for the Client File Retrieval Configuration feature, including entities, value objects, relationships, validation rules, and state transitions. The model follows Domain-Driven Design principles and aligns with RiskInsure architectural patterns (repository pattern, single-partition model, optimistic concurrency).

---

## Core Entities

### 1. FileRetrievalConfiguration

**Purpose**: Represents a configured file check for a client, defining WHERE to look, WHEN to look, and WHAT TO DO when files are found.

**Properties**:

| Property | Type | Required | Description | Validation |
|----------|------|----------|-------------|------------|
| `Id` | `Guid` | Yes | Unique configuration identifier | Not empty |
| `ClientId` | `string` | Yes | Client owning this configuration | Not empty, max 50 chars |
| `Name` | `string` | Yes | Human-readable configuration name | Not empty, max 200 chars |
| `Description` | `string?` | No | Description of purpose | Max 1000 chars |
| `Protocol` | `ProtocolType` | Yes | FTP, HTTPS, AzureBlob | Valid enum value |
| `ProtocolSettings` | `ProtocolSettings` | Yes | Protocol-specific connection settings | Valid for protocol type |
| `FilePathPattern` | `string` | Yes | Path pattern with optional tokens | Not empty, max 500 chars, valid path |
| `FilenamePattern` | `string` | Yes | Filename pattern with optional tokens | Not empty, max 200 chars |
| `FileExtension` | `string?` | No | File extension filter (e.g., "xlsx", "pdf") | Max 10 chars, alphanumeric only |
| `Schedule` | `ScheduleDefinition` | Yes | When to execute file checks | Valid cron or schedule format |
| `EventsToPublish` | `List<EventDefinition>` | Yes | Events to publish when files found | At least 1 event |
| `CommandsToSend` | `List<CommandDefinition>` | No | Commands to send when files found | Max 10 commands |
| `IsActive` | `bool` | Yes | Whether configuration is active | Default: true |
| `CreatedAt` | `DateTimeOffset` | Yes | When configuration was created | Not future date |
| `CreatedBy` | `string` | Yes | User who created configuration | Not empty |
| `LastModifiedAt` | `DateTimeOffset?` | No | Last update timestamp | Not before CreatedAt |
| `LastModifiedBy` | `string?` | No | User who last updated | - |
| `LastExecutedAt` | `DateTimeOffset?` | No | Last successful execution | - |
| `NextScheduledRun` | `DateTimeOffset?` | No | Next planned execution (calculated) | - |
| `ETag` | `string` | Yes | Optimistic concurrency token | Cosmos DB managed |

**Relationships**:
- One-to-Many with `FileRetrievalExecution` (one configuration → many executions)
- One-to-Many with `DiscoveredFile` (one configuration → many discovered files)

**Storage**:
- Cosmos DB container: `file-retrieval-configurations`
- Partition key: `/clientId`
- Document type discriminator: `FileRetrievalConfiguration`

**Invariants**:
- FilePathPattern and FilenamePattern must not contain tokens in server/host portion (validated during creation)
- At least one EventDefinition must be present
- Schedule must be valid cron expression or structured schedule
- ProtocolSettings must match Protocol type (e.g., FtpSettings for FTP)
- IsActive = false prevents scheduled execution but does not delete configuration

**State Transitions**: Not applicable (configuration does not have state machine)

---

### 2. FileRetrievalExecution

**Purpose**: Represents a single execution attempt of a FileRetrievalConfiguration, tracking success/failure and discovered files.

**Properties**:

| Property | Type | Required | Description | Validation |
|----------|------|----------|-------------|------------|
| `Id` | `Guid` | Yes | Unique execution identifier | Not empty |
| `ClientId` | `string` | Yes | Client owning this execution | Not empty |
| `ConfigurationId` | `Guid` | Yes | Configuration that was executed | Valid configuration exists |
| `ExecutionStartedAt` | `DateTimeOffset` | Yes | When execution started | Not future date |
| `ExecutionCompletedAt` | `DateTimeOffset?` | No | When execution finished | Not before StartedAt |
| `Status` | `ExecutionStatus` | Yes | Pending, InProgress, Completed, Failed | Valid enum value |
| `FilesFound` | `int` | Yes | Number of files discovered | Non-negative |
| `FilesProcessed` | `int` | Yes | Number of files for which events published | Non-negative, <= FilesFound |
| `ErrorMessage` | `string?` | No | Error details if failed | Max 5000 chars |
| `ErrorCategory` | `string?` | No | Error type (AuthenticationFailure, etc.) | Max 100 chars |
| `ResolvedFilePathPattern` | `string` | Yes | Path pattern after token replacement | Not empty |
| `ResolvedFilenamePattern` | `string` | Yes | Filename pattern after token replacement | Not empty |
| `DurationMs` | `long` | Yes | Execution duration in milliseconds | Non-negative |
| `RetryCount` | `int` | Yes | Number of retry attempts | Non-negative, max 3 |

**Relationships**:
- Many-to-One with `FileRetrievalConfiguration`
- One-to-Many with `DiscoveredFile` (one execution → many files discovered)

**Storage**:
- Cosmos DB container: `file-retrieval-executions`
- Partition key: `/clientId/configurationId` (hierarchical)
- Document type discriminator: `FileRetrievalExecution`
- TTL: 90 days (per spec assumption)

**State Transitions**:
```
Pending → InProgress → Completed
         → InProgress → Failed → InProgress (retry, max 3 attempts)
         → InProgress → Failed (terminal, after max retries)
```

**Invariants**:
- ExecutionCompletedAt required when Status is Completed or Failed (terminal)
- ErrorMessage required when Status is Failed
- FilesProcessed must be <= FilesFound
- RetryCount must not exceed 3 (per research: 3 retries with exponential backoff)

---

### 3. DiscoveredFile

**Purpose**: Represents a file found during a retrieval check, used for idempotency tracking to prevent duplicate workflow triggers (SC-007).

**Properties**:

| Property | Type | Required | Description | Validation |
|----------|------|----------|-------------|------------|
| `Id` | `Guid` | Yes | Unique discovered file identifier | Not empty |
| `ClientId` | `string` | Yes | Client owning this file | Not empty |
| `ConfigurationId` | `Guid` | Yes | Configuration that discovered file | Not empty |
| `ExecutionId` | `Guid` | Yes | Execution that discovered file | Not empty |
| `FileUrl` | `string` | Yes | Full file location/URL | Not empty, max 2000 chars |
| `Filename` | `string` | Yes | File name only | Not empty, max 255 chars |
| `FileSize` | `long?` | No | File size in bytes | Non-negative |
| `LastModified` | `DateTimeOffset?` | No | File last modified timestamp | - |
| `DiscoveredAt` | `DateTimeOffset` | Yes | When file was discovered | Not future date |
| `DiscoveryDate` | `DateOnly` | Yes | Discovery date (for idempotency) | Not future date |
| `Status` | `DiscoveryStatus` | Yes | Pending, EventPublished, Failed | Valid enum value |
| `EventPublishedAt` | `DateTimeOffset?` | No | When FileDiscovered event was published | - |
| `ProcessingError` | `string?` | No | Error if event publish failed | Max 2000 chars |

**Relationships**:
- Many-to-One with `FileRetrievalConfiguration`
- Many-to-One with `FileRetrievalExecution`

**Storage**:
- Cosmos DB container: `discovered-files`
- Partition key: `/clientId/configurationId` (hierarchical)
- Document type discriminator: `DiscoveredFile`
- Unique key constraint: `(clientId, configurationId, fileUrl, discoveryDate)` - **Idempotency enforcement**
- TTL: 90 days (aligned with execution history retention)

**State Transitions**:
```
Pending → EventPublished (terminal)
        → Failed (terminal, after retries)
```

**Invariants**:
- EventPublishedAt required when Status is EventPublished
- ProcessingError required when Status is Failed
- Unique key constraint enforces: same file on same day = single discovery record (idempotency)

---

## Value Objects

### 1. ProtocolSettings (Abstract Base)

**Purpose**: Base class for protocol-specific connection settings. Concrete implementations per protocol.

**Properties**:

| Property | Type | Required | Description | Validation |
|----------|------|----------|-------------|------------|
| `ProtocolType` | `ProtocolType` | Yes | FTP, HTTPS, AzureBlob | Valid enum value |

**Derived Types**:

#### 1.1 FtpProtocolSettings : ProtocolSettings

| Property | Type | Required | Description | Validation |
|----------|------|----------|-------------|------------|
| `Server` | `string` | Yes | FTP server address (hostname or IP) | Not empty, max 255 chars |
| `Port` | `int` | Yes | FTP port | 1-65535, default 21 |
| `Username` | `string` | Yes | FTP username | Not empty, max 100 chars |
| `PasswordKeyVaultSecret` | `string` | Yes | Azure Key Vault secret name for password | Not empty |
| `UseTls` | `bool` | Yes | Enable FTPS (FTP over TLS) | Default: true |
| `UsePassiveMode` | `bool` | Yes | Passive mode (NAT/firewall friendly) | Default: true |
| `ConnectionTimeout` | `TimeSpan` | Yes | Connection timeout | Positive, default 30 seconds |

#### 1.2 HttpsProtocolSettings : ProtocolSettings

| Property | Type | Required | Description | Validation |
|----------|------|----------|-------------|------------|
| `BaseUrl` | `string` | Yes | Base HTTPS URL (scheme + host) | Valid HTTPS URL, max 500 chars |
| `AuthenticationType` | `AuthType` | Yes | None, BasicAuth, BearerToken, ApiKey | Valid enum value |
| `UsernameOrApiKey` | `string?` | No | Username (BasicAuth) or API key | Max 200 chars |
| `PasswordOrTokenKeyVaultSecret` | `string?` | No | Key Vault secret for password/token | Max 200 chars |
| `ConnectionTimeout` | `TimeSpan` | Yes | Connection timeout | Positive, default 30 seconds |
| `FollowRedirects` | `bool` | Yes | Follow HTTP redirects | Default: true |
| `MaxRedirects` | `int` | Yes | Max redirect hops | 0-10, default 3 |

#### 1.3 AzureBlobProtocolSettings : ProtocolSettings

| Property | Type | Required | Description | Validation |
|----------|------|----------|-------------|------------|
| `StorageAccountName` | `string` | Yes | Azure Storage account name | Not empty, max 24 chars |
| `ContainerName` | `string` | Yes | Blob container name | Not empty, max 63 chars, lowercase |
| `AuthenticationType` | `AzureAuthType` | Yes | ManagedIdentity, ConnectionString, SasToken | Valid enum value |
| `ConnectionStringKeyVaultSecret` | `string?` | No | Key Vault secret for connection string | Required if ConnectionString auth |
| `SasTokenKeyVaultSecret` | `string?` | No | Key Vault secret for SAS token | Required if SasToken auth |
| `BlobPrefix` | `string?` | No | Prefix for blob listing (path filter) | Max 1024 chars |

---

### 2. ScheduleDefinition

**Purpose**: Defines when a FileRetrievalConfiguration should execute.

**Properties**:

| Property | Type | Required | Description | Validation |
|----------|------|----------|-------------|------------|
| `CronExpression` | `string` | Yes | Cron expression (standard 5-field format) | Valid cron syntax |
| `Timezone` | `string` | Yes | IANA timezone (e.g., "America/New_York") | Valid timezone identifier |
| `Description` | `string?` | No | Human-readable schedule description | Max 200 chars |

**Cron Expression Format**: Standard 5-field cron
```
* * * * *
│ │ │ │ │
│ │ │ │ └─── Day of week (0-7, 0 and 7 are Sunday)
│ │ │ └───── Month (1-12)
│ │ └─────── Day of month (1-31)
│ └───────── Hour (0-23)
└─────────── Minute (0-59)
```

**Examples**:
- `"0 8 * * *"` - Daily at 8:00 AM
- `"0 */6 * * *"` - Every 6 hours
- `"0 8 * * 1"` - Every Monday at 8:00 AM
- `"30 14 1 * *"` - First day of every month at 2:30 PM

**Validation**: Use NCrontab library to parse and validate cron expression during configuration creation.

---

### 3. EventDefinition

**Purpose**: Defines an event to publish when files are discovered.

**Properties**:

| Property | Type | Required | Description | Validation |
|----------|------|----------|-------------|------------|
| `EventType` | `string` | Yes | Event message type (e.g., "FileDiscovered") | Not empty, max 200 chars |
| `EventData` | `Dictionary<string, object>?` | No | Additional static event data | Max 10 KB serialized |

**Usage**: When a file is discovered, system publishes event with type `EventType` and includes:
- File metadata (URL, filename, size, discovery timestamp)
- ConfigurationId, ClientId (from configuration)
- Static EventData (from definition)

---

### 4. CommandDefinition

**Purpose**: Defines a command to send to the workflow orchestration platform when files are discovered.

**Properties**:

| Property | Type | Required | Description | Validation |
|----------|------|----------|-------------|------------|
| `CommandType` | `string` | Yes | Command message type (e.g., "ProcessFile") | Not empty, max 200 chars |
| `TargetEndpoint` | `string` | Yes | NServiceBus endpoint name | Not empty, max 200 chars |
| `CommandData` | `Dictionary<string, object>?` | No | Additional static command data | Max 10 KB serialized |

**Usage**: When a file is discovered, system sends command with type `CommandType` to `TargetEndpoint` and includes:
- File metadata (URL, filename, size, discovery timestamp)
- ConfigurationId, ClientId (from configuration)
- Static CommandData (from definition)

---

## Enumerations

### ProtocolType
```csharp
public enum ProtocolType
{
    Ftp,        // FTP or FTPS
    Https,      // HTTPS endpoints
    AzureBlob   // Azure Blob Storage
    // Future: AwsS3, Sftp, SharePoint, GoogleCloudStorage
}
```

### ExecutionStatus
```csharp
public enum ExecutionStatus
{
    Pending,     // Scheduled but not yet started
    InProgress,  // Currently executing
    Completed,   // Successfully completed (terminal)
    Failed       // Failed after retries (terminal)
}
```

### DiscoveryStatus
```csharp
public enum DiscoveryStatus
{
    Pending,         // File discovered, event not yet published
    EventPublished,  // FileDiscovered event published (terminal)
    Failed           // Event publish failed after retries (terminal)
}
```

### AuthType (for HTTPS)
```csharp
public enum AuthType
{
    None,        // No authentication
    BasicAuth,   // HTTP Basic Authentication
    BearerToken, // Bearer token (Authorization: Bearer {token})
    ApiKey       // API key in header or query string
}
```

### AzureAuthType (for Azure Blob Storage)
```csharp
public enum AzureAuthType
{
    ManagedIdentity,  // Azure Managed Identity (preferred)
    ConnectionString, // Storage account connection string
    SasToken          // Shared Access Signature token
}
```

---

## Entity Relationships

```
FileRetrievalConfiguration (1) ----< (M) FileRetrievalExecution
                                         |
                                         +----< (M) DiscoveredFile

FileRetrievalConfiguration (1) ----< (M) DiscoveredFile (direct)
```

**Explanation**:
- One configuration has many executions (historical record)
- One execution discovers many files (during single check)
- One configuration has many discovered files across all executions (cumulative)

---

## Validation Rules

### Cross-Entity Validation

1. **FileRetrievalConfiguration Creation**:
   - ClientId must exist in client management system (external validation)
   - ProtocolSettings must match ProtocolType (e.g., FtpSettings for FTP)
   - CronExpression must be valid (validated by NCrontab parser)
   - FilePathPattern and FilenamePattern must not contain tokens in server/host portion
   - At least one EventDefinition must be present
   - If AuthenticationType requires credentials, Key Vault secret names must be provided

2. **Execution Start**:
   - Configuration must be Active (IsActive = true)
   - Configuration must exist and be accessible (clientId scope)
   - Cannot start execution if configuration is deleted

3. **File Discovery**:
   - FileUrl must be unique per (clientId, configurationId, discoveryDate) - enforced by unique key
   - Duplicate file on same day returns early (idempotent)
   - Duplicate file on different day creates new DiscoveredFile record

4. **Token Replacement**:
   - Tokens validated during configuration creation (cannot be in server name)
   - Token replacement must produce valid paths (no validation errors at runtime)
   - Unsupported tokens (e.g., `{clientId}`, `{custom}`) rejected during creation

---

## State Transition Matrix

### FileRetrievalExecution State Transitions

| From State | To State | Trigger | Validation |
|------------|----------|---------|------------|
| Pending | InProgress | ExecuteFileCheck command received | Configuration is active |
| InProgress | Completed | File check succeeds | FilesFound >= 0, events published |
| InProgress | Failed | File check fails (transient error) | Retry count < 3 (retry) |
| InProgress | Failed | File check fails (terminal error) | Retry count >= 3 or non-retryable error |
| Failed | InProgress | Retry triggered | Retry count < 3 |

### DiscoveredFile State Transitions

| From State | To State | Trigger | Validation |
|------------|----------|---------|------------|
| Pending | EventPublished | FileDiscovered event published | Event successfully sent to Service Bus |
| Pending | Failed | Event publish fails | After retry attempts exhausted |

---

## Cosmos DB Document Structure

### FileRetrievalConfiguration Document Example

```json
{
  "id": "e3a85f64-5717-4562-b3fc-2c963f66afa6",
  "clientId": "client-acme-corp",
  "documentType": "FileRetrievalConfiguration",
  "name": "Daily Transaction Files",
  "description": "Check for daily transaction files from ACME Corp FTP",
  "protocol": "Ftp",
  "protocolSettings": {
    "protocolType": "Ftp",
    "server": "ftp.acmecorp.com",
    "port": 21,
    "username": "riskinsure_user",
    "passwordKeyVaultSecret": "acme-ftp-password",
    "useTls": true,
    "usePassiveMode": true,
    "connectionTimeout": "00:00:30"
  },
  "filePathPattern": "/transactions/{yyyy}/{mm}",
  "filenamePattern": "trans_{yyyy}{mm}{dd}.csv",
  "fileExtension": "csv",
  "schedule": {
    "cronExpression": "0 8 * * *",
    "timezone": "America/New_York",
    "description": "Daily at 8:00 AM ET"
  },
  "eventsToPublish": [
    {
      "eventType": "FileDiscovered",
      "eventData": {
        "fileType": "Transaction",
        "priority": "High"
      }
    }
  ],
  "commandsToSend": [
    {
      "commandType": "ProcessTransactionFile",
      "targetEndpoint": "WorkflowOrchestrator",
      "commandData": {
        "workflowType": "TransactionProcessing"
      }
    }
  ],
  "isActive": true,
  "createdAt": "2025-01-24T10:00:00Z",
  "createdBy": "admin@riskinsure.com",
  "lastModifiedAt": "2025-01-24T10:00:00Z",
  "lastModifiedBy": "admin@riskinsure.com",
  "lastExecutedAt": "2025-01-25T13:00:00Z",
  "nextScheduledRun": "2025-01-26T13:00:00Z",
  "_etag": "\"0000d00a-0000-0000-0000-000000000000\""
}
```

### FileRetrievalExecution Document Example

```json
{
  "id": "a1b2c3d4-1234-5678-abcd-1234567890ab",
  "clientId": "client-acme-corp",
  "configurationId": "e3a85f64-5717-4562-b3fc-2c963f66afa6",
  "documentType": "FileRetrievalExecution",
  "executionStartedAt": "2025-01-25T13:00:00Z",
  "executionCompletedAt": "2025-01-25T13:00:05Z",
  "status": "Completed",
  "filesFound": 1,
  "filesProcessed": 1,
  "errorMessage": null,
  "errorCategory": null,
  "resolvedFilePathPattern": "/transactions/2025/01",
  "resolvedFilenamePattern": "trans_20250125.csv",
  "durationMs": 5234,
  "retryCount": 0
}
```

### DiscoveredFile Document Example

```json
{
  "id": "f9876543-abcd-1234-5678-1234567890ab",
  "clientId": "client-acme-corp",
  "configurationId": "e3a85f64-5717-4562-b3fc-2c963f66afa6",
  "executionId": "a1b2c3d4-1234-5678-abcd-1234567890ab",
  "documentType": "DiscoveredFile",
  "fileUrl": "ftp://ftp.acmecorp.com/transactions/2025/01/trans_20250125.csv",
  "filename": "trans_20250125.csv",
  "fileSize": 524288,
  "lastModified": "2025-01-25T06:00:00Z",
  "discoveredAt": "2025-01-25T13:00:03Z",
  "discoveryDate": "2025-01-25",
  "status": "EventPublished",
  "eventPublishedAt": "2025-01-25T13:00:04Z",
  "processingError": null
}
```

---

## Data Integrity Constraints

1. **Optimistic Concurrency**: All updates to FileRetrievalConfiguration use ETag checks (prevent lost updates)
2. **Partition Co-location**: 
   - Configurations partitioned by `/clientId` (client-scoped queries)
   - Executions and DiscoveredFiles partitioned by `/clientId/configurationId` (hierarchical, co-located)
3. **Unique Key Constraint**: DiscoveredFile has unique key on `(clientId, configurationId, fileUrl, discoveryDate)` for idempotency
4. **Referential Integrity**: ConfigurationId in executions and discovered files must reference existing configuration (validated at creation)
5. **Immutable History**: FileRetrievalExecution and DiscoveredFile are append-only (never updated after terminal state)

---

## Performance Considerations

1. **Query Patterns**:
   - Most common: List configurations for client (partition key query = efficient)
   - Execution history: Query by configurationId (hierarchical partition key)
   - Dashboard queries: Cross-partition (acceptable for occasional admin use)
   - Active configuration polling: Secondary index on `isActive` + `nextScheduledRun`

2. **Document Size**:
   - FileRetrievalConfiguration: Typically < 10 KB (small, metadata only)
   - FileRetrievalExecution: < 5 KB per execution (metadata only)
   - DiscoveredFile: < 2 KB per file (metadata only, no file content stored)
   - EventData/CommandData: Limited to 10 KB (warn at 5 KB)

3. **Indexing**:
   - Partition key: Automatic (efficient queries)
   - Secondary indexes: 
     - `isActive` (for scheduler queries)
     - `nextScheduledRun` (for scheduler queries)
     - `status` (for filtering active/inactive configs)
   - Exclude large fields from indexing: EventData, CommandData (use default indexing policy)

4. **TTL (Time-To-Live)**:
   - `file-retrieval-executions`: 90 days (spec assumption)
   - `discovered-files`: 90 days (aligned with executions)
   - Configurations: No TTL (retained indefinitely, soft-delete via IsActive)

---

## Security Considerations

1. **Credential Storage**: All passwords, tokens, connection strings stored in **Azure Key Vault**, not Cosmos DB
   - Configuration stores Key Vault secret name (e.g., `"acme-ftp-password"`)
   - FileCheckService retrieves secret at execution time
   - Never log or expose credentials in error messages

2. **Client Isolation**: Partition key `/clientId` enforces data isolation
   - Queries always include clientId (enforced by repository pattern)
   - Cross-client access prevented at data layer

3. **API Security**: REST API endpoints validate clientId from JWT claims (not request body)
   - User can only access configurations for their assigned clientId
   - Unauthorized access returns 403 Forbidden

---

## Summary

This data model provides:

✅ **Clear entity boundaries** with well-defined responsibilities  
✅ **Comprehensive validation rules** at entity and cross-entity levels  
✅ **Explicit state transitions** with validation rules (executions and discoveries)  
✅ **Optimistic concurrency** for safe concurrent updates (FileRetrievalConfiguration)  
✅ **Idempotency enforcement** via unique key constraint (DiscoveredFile)  
✅ **Multi-tenant isolation** via partition key design (`/clientId`, `/clientId/configurationId`)  
✅ **Extensible protocol architecture** via ProtocolSettings inheritance  
✅ **Security-first design** (Key Vault integration, client-scoped access)  
✅ **Performance optimization** via co-location and indexing strategy  

Ready to proceed to **contracts generation** (commands and events).
