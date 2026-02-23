# Research: Client File Retrieval Configuration

**Date**: 2025-01-24  
**Feature**: 001-file-retrieval-config  
**Phase**: Phase 0 - Research

## Overview

This document captures research findings, architectural decisions, and technology choices for the Client File Retrieval Configuration feature. Each section addresses key unknowns identified in the Technical Context (marked as "NEEDS CLARIFICATION") and provides rationale for decisions made.

---

## 1. Schedule Execution Mechanism

### Research Question
What is the best approach for executing file checks on configured schedules? Options: Quartz.NET, NCrontab, Azure Functions Timer, Hangfire, custom solution?

### Decision: NCrontab + Background Hosted Service (HostedService)

### Rationale

**Option 1: Quartz.NET**
- ✅ Full-featured enterprise job scheduler
- ✅ Persistent job storage (survive restarts)
- ✅ Clustering support for distributed execution
- ✅ Cron expressions, calendars, triggers
- ❌ Heavy dependency (large library, complex setup)
- ❌ Requires persistent store (adds complexity to Cosmos DB schema)
- ❌ Overkill for simple scheduled checks

**Option 2: Azure Functions Timer Triggers**
- ✅ Built-in scheduling infrastructure
- ✅ Serverless scaling
- ❌ Not aligned with Container Apps architecture (constitution mandates Container Apps, not Functions)
- ❌ Constitution explicitly prohibits Azure Functions: "❌ Azure Functions (use Azure Container Apps)"

**Option 3: Hangfire**
- ✅ Simple API, persistent background jobs
- ✅ Dashboard for monitoring
- ❌ Requires SQL Server or Redis (not Cosmos DB)
- ❌ Another persistence store to manage
- ❌ Complexity for simple periodic checks

**Chosen: NCrontab + .NET HostedService (Background Worker)**
- ✅ Lightweight library (just cron parsing)
- ✅ Aligns with Container Apps + .NET architecture
- ✅ Simple to implement: `BackgroundService` with timer
- ✅ No additional persistence required (schedules stored in FileRetrievalConfiguration)
- ✅ Can run multiple worker instances (horizontal scaling)
- ✅ Cron expression support: `"0 8 * * *"` for "daily at 8 AM"
- ❌ Manual implementation of persistence and clustering (acceptable for MVP)

### Architecture Pattern

```
FileRetrieval.Worker (Container App)
  ↓
SchedulerHostedService : BackgroundService
  ↓ (every minute)
ScheduleEvaluator (uses NCrontab)
  ↓ (checks all configurations)
For each due configuration:
  Send ExecuteFileCheck command via NServiceBus
    ↓
ExecuteFileCheckHandler (NServiceBus handler)
  ↓
FileCheckService.ExecuteCheck()
  ↓ (delegates to protocol adapter)
IProtocolAdapter (FTP/HTTPS/Azure Blob)
  ↓
Publishes FileDiscovered events
```

### Implementation Notes

1. **SchedulerHostedService** runs as a `BackgroundService` in `FileRetrieval.Worker`
2. Polls active FileRetrievalConfigurations every minute (configurable interval)
3. Uses **NCrontab** to parse cron expressions and determine next run time
4. Tracks last execution time in `FileRetrievalExecution` records
5. Publishes `ExecuteFileCheck` command for each due configuration
6. **Horizontal scaling**: Multiple worker instances can run concurrently
   - Use distributed lock (Cosmos DB lease) to prevent duplicate checks
   - OR rely on idempotent handlers (check for existing execution before processing)

### Alternatives Considered

- **Custom timer loop**: Rejected due to lack of cron expression support
- **Azure Durable Functions**: Rejected due to constitution constraint (no Azure Functions)
- **Temporal.io / Cadence**: Rejected as overkill for simple scheduling

### Libraries Required

- **NCrontab** (NuGet: `NCrontab`) - Cron expression parsing and evaluation
- **Microsoft.Extensions.Hosting** - Background service hosting (built-in .NET)

---

## 2. FTP Client Library Selection

### Research Question
Which .NET FTP library should we use for FTP protocol support? Security, reliability, and async support are critical.

### Decision: FluentFTP (MIT License)

### Rationale

**Option 1: FluentFTP**
- ✅ Modern async/await API (fully async)
- ✅ FTP, FTPS (TLS/SSL), SFTP support
- ✅ Active maintenance (2024+ updates)
- ✅ MIT License (permissive)
- ✅ Excellent documentation and examples
- ✅ Supports passive/active modes, proxy, resume
- ✅ Stream-based API (efficient for large files)
- ✅ NuGet package: `FluentFTP` (3M+ downloads)

**Option 2: SSH.NET (for SFTP only)**
- ✅ SFTP support (SSH-based file transfer)
- ✅ Active maintenance
- ❌ No FTP/FTPS support (different protocol)
- ❌ Would need separate library for FTP
- ⚠️ SFTP is different from FTPS (SSH vs TLS)

**Option 3: System.Net.FtpWebRequest (built-in .NET)**
- ✅ Built-in, no external dependency
- ❌ **Obsolete** since .NET 5 (deprecated)
- ❌ Synchronous API only (poor performance)
- ❌ Limited FTPS support
- ❌ Poor error handling

**Chosen: FluentFTP**
- ✅ Best async support for .NET
- ✅ Covers FTP, FTPS, and SFTP (all protocols clients may use)
- ✅ Reliable, well-maintained
- ✅ Permissive license

### Implementation Notes

1. **FtpProtocolAdapter** wraps FluentFTP's `AsyncFtpClient`
2. Support for FTPS (TLS/SSL) is enabled by default for security
3. Passive mode by default (works through firewalls/NAT)
4. Connection pooling or connection reuse for efficiency
5. Timeout configuration: 30 seconds for connection, 60 seconds for data transfer
6. Retry logic: 3 attempts with exponential backoff for transient errors

### Configuration Settings

```csharp
public class FtpProtocolSettings
{
    public string Server { get; set; }        // ftp.example.com
    public int Port { get; set; } = 21;       // Default FTP port
    public string Username { get; set; }
    public string Password { get; set; }      // TODO: Use Azure Key Vault
    public bool UseTls { get; set; } = true;  // FTPS recommended
    public bool UsePassiveMode { get; set; } = true;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

### Security Considerations

- Store FTP credentials in **Azure Key Vault** (not in Cosmos DB directly)
- Use FTPS (FTP over TLS) whenever possible
- Support certificate validation (avoid accepting all certificates)
- Log connection attempts but NOT passwords

---

## 3. Cosmos DB Partition Key Strategy

### Research Question
Should FileRetrievalConfiguration be partitioned by `clientId` alone or use hierarchical key `/clientId/configurationId`?

### Decision: Single Partition Key `/clientId`

### Rationale

**Option 1: Partition by `clientId`**
- ✅ Free queries for all configurations within a client (most common query)
- ✅ Client-scoped security trimming is efficient (single partition query)
- ✅ Tenant-level operations (list all configs for client) are efficient
- ✅ Aligns with multi-tenancy model
- ❌ Could create hot partitions if one client has many configurations (mitigated: max 20 per client per spec)
- ❌ Queries for single configuration still need full key (clientId + configurationId)

**Option 2: Hierarchical Partition Key `/clientId/configurationId`**
- ✅ Perfect isolation per configuration
- ✅ Free queries within single configuration (execution history, discovered files)
- ❌ Client-level queries (list all configs for client) require cross-partition query
- ❌ Adds complexity for common operations
- ❌ Configuration is lightweight (not execution-heavy like workflow instances)

**Chosen: `/clientId`**
- ✅ Most queries are "list configurations for client" (API CRUD, security trimming)
- ✅ Execution history is separate concern (can use different partition strategy)
- ✅ Configuration count per client is bounded (max 20 per spec)
- ✅ Aligns with existing workflow orchestration pattern (tenant-level partitioning)

### Partition Strategy Per Container

| Container | Partition Key | Rationale |
|-----------|---------------|-----------|
| `file-retrieval-configurations` | `/clientId` | Client-scoped queries, bounded per client |
| `file-retrieval-executions` | `/clientId/configurationId` (hierarchical) | Execution history per configuration, high volume |
| `discovered-files` | `/clientId/configurationId` (hierarchical) | Co-locate with executions, idempotency checks |

### Implementation Notes

1. **Configurations**: `/clientId` - supports API CRUD, security trimming
2. **Executions**: `/clientId/configurationId` - high-volume history, per-config queries
3. **DiscoveredFiles**: `/clientId/configurationId` - idempotency tracking, co-located with executions
4. All documents include `documentType` discriminator field
5. Secondary indexes: `status` (active/inactive), `nextScheduledRun` (for scheduler queries)

---

## 4. Protocol-Specific Best Practices

### 4.1 FTP Best Practices

**Connection Management**:
- Use connection pooling for frequent checks (avoid connection overhead)
- Close connections properly in finally blocks
- Implement exponential backoff for transient errors (network timeouts)

**File Listing**:
- Use `GetListing()` with wildcards: `*.xlsx`, `report-*.pdf`
- Filter by date tokens AFTER listing (token replacement → filter)
- Avoid recursive directory scans (performance impact)

**Security**:
- Prefer FTPS (FTP over TLS) over plain FTP
- Validate server certificates (do not accept all certificates in production)
- Store credentials in Azure Key Vault (not in configuration)

**Error Handling**:
- Retry on transient errors: `IOException`, `SocketException`
- Log authentication failures (do not retry with same credentials)
- Handle "file not found" gracefully (not an error if optional)

### 4.2 HTTPS Best Practices

**HTTP Client Usage**:
- Use **HttpClient** with `IHttpClientFactory` (avoid socket exhaustion)
- Set appropriate timeouts: 30 seconds for connection, 60 seconds for response
- Follow redirects (up to 3 hops)

**Authentication**:
- Support Basic Auth, Bearer Token, API Key
- Store credentials in Azure Key Vault
- Include `User-Agent` header (identify as "RiskInsure File Retrieval")

**File Checking**:
- Use **HEAD** request first to check file existence (lightweight)
- Use **GET** only if HEAD fails or metadata needed
- Check `Content-Length` header for file size
- Check `Last-Modified` or `ETag` for file changes

**Error Handling**:
- Retry on 5xx errors (server errors)
- Do NOT retry on 4xx errors (client errors - misconfiguration)
- Handle 404 gracefully (file not found)

**Performance**:
- Enable HTTP/2 if supported (multiplexing)
- Use connection pooling (built-in with IHttpClientFactory)

### 4.3 Azure Blob Storage Best Practices

**SDK Usage**:
- Use **Azure.Storage.Blobs** SDK (latest stable version)
- Use `BlobContainerClient.GetBlobsAsync()` for listing
- Authenticate with Managed Identity (preferred) or Connection String

**File Listing**:
- Use `GetBlobsAsync()` with prefix filter: `blobs/2025/01/`
- Filter by token-replaced path (efficient server-side filtering)
- Use `BlobTraits.Metadata` to include metadata in listing (avoid extra calls)

**Authentication**:
- Prefer **Azure Managed Identity** (no credentials in config)
- Fallback to **Connection String** stored in Azure Key Vault
- Use **SAS tokens** for limited-time access (delegated permissions)

**Performance**:
- Use async methods (`GetBlobsAsync`, not synchronous `GetBlobs`)
- Enable blob listing pagination (default 5000 blobs per page)
- Cache `BlobServiceClient` instances (reuse connections)

**Error Handling**:
- Retry on `RequestFailedException` with status 5xx (transient)
- Handle `404` gracefully (blob not found)
- Check for `403 Forbidden` (permission issues - log for troubleshooting)

---

## 5. Date Token Replacement Strategy

### Research Question
How should we implement date token replacement (`{yyyy}`, `{mm}`, `{dd}`, `{yy}`) to ensure 100% accuracy (SC-008)?

### Decision: Explicit Token Replacement Service with Validation

### Rationale

**Token Definitions**:
- `{yyyy}` → 4-digit year (e.g., `2025`)
- `{yy}` → 2-digit year (e.g., `25`)
- `{mm}` → 2-digit month with leading zero (e.g., `01`, `12`)
- `{dd}` → 2-digit day with leading zero (e.g., `01`, `31`)

**Implementation Approach**:
1. **TokenReplacementService** (domain service)
2. Accepts: file pattern string, execution timestamp
3. Returns: resolved string with tokens replaced
4. Validates: tokens are in expected positions (path/filename, not server name)
5. Throws: `InvalidTokenException` if unsupported token positions

**Edge Cases**:
- Invalid dates from tokens: N/A (tokens always produce valid dates based on execution time)
- Future dates: Tokens use `DateTimeOffset.UtcNow` at execution time (always current date)
- Timezone handling: Use **UTC** for consistency across distributed workers
- Missing tokens: Valid (configuration may not use tokens)

### Implementation

```csharp
public class TokenReplacementService
{
    public string ReplaceTokens(string pattern, DateTimeOffset executionTime)
    {
        // Validate pattern (tokens only in path/filename, not server/domain)
        ValidateTokenPositions(pattern);
        
        var result = pattern;
        result = result.Replace("{yyyy}", executionTime.Year.ToString("D4"));
        result = result.Replace("{yy}", (executionTime.Year % 100).ToString("D2"));
        result = result.Replace("{mm}", executionTime.Month.ToString("D2"));
        result = result.Replace("{dd}", executionTime.Day.ToString("D2"));
        
        return result;
    }
    
    private void ValidateTokenPositions(string pattern)
    {
        // Ensure tokens not in server name (e.g., ftp://{yyyy}.example.com - INVALID)
        // Allow tokens in path/filename only
        var uri = TryParseUri(pattern);
        if (uri != null && uri.Host.Contains("{"))
            throw new InvalidTokenException("Tokens not allowed in server/host name");
    }
}
```

### Test Cases (for 90%+ domain coverage)

1. All tokens: `https://example.com/files/{yyyy}/{mm}/{dd}/report-{yy}.xlsx`
2. Partial tokens: `ftp://ftp.example.com/reports/{yyyy}-{mm}.csv`
3. No tokens: `https://example.com/static/file.pdf`
4. Invalid positions: `https://{yyyy}.example.com/file.pdf` (should throw)
5. Month boundaries: Execution on 2025-01-01, 2025-12-31
6. Leap year: Execution on 2024-02-29
7. Case sensitivity: `{YYYY}` vs `{yyyy}` (only lowercase supported)

---

## 6. Idempotency and Duplicate Prevention

### Research Question
How do we achieve 100% idempotency (SC-007) to prevent duplicate workflow triggers for the same file?

### Decision: DiscoveredFile Tracking with Composite Key

### Rationale

**Idempotency Key**: `clientId + configurationId + fileUrl + discoveryDate`
- Uniquely identifies a file discovery event
- Prevents duplicate triggers for the same file on the same day
- Allows re-processing on different days (if needed)

**Implementation Pattern**:
1. Before publishing `FileDiscovered` event, check for existing `DiscoveredFile` record
2. Use Cosmos DB unique key constraint: `(clientId, configurationId, fileUrl, discoveryDate)`
3. If record exists, log and skip (return early - idempotent)
4. If record does not exist, create record and publish event (atomic via ETag)

**ETag-Based Atomicity**:
```csharp
// Attempt to create DiscoveredFile record
var discoveredFile = new DiscoveredFile 
{ 
    ClientId = config.ClientId,
    ConfigurationId = config.Id,
    FileUrl = fileUrl,
    DiscoveryDate = DateOnly.FromDateTime(DateTime.UtcNow),
    DiscoveredAt = DateTimeOffset.UtcNow,
    Status = DiscoveryStatus.Pending
};

try 
{
    await _repository.CreateAsync(discoveredFile);
    // Only publish event if creation succeeds (idempotency enforced by unique key)
    await _messageBus.Publish(new FileDiscovered { ... });
}
catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
{
    // Record already exists - duplicate detection
    _logger.LogInformation("File already discovered: {FileUrl}", fileUrl);
    return; // Idempotent - no event published
}
```

### Cosmos DB Unique Key Constraint

Container: `discovered-files`
```json
{
  "uniqueKeyPolicy": {
    "uniqueKeys": [
      {
        "paths": [
          "/clientId",
          "/configurationId",
          "/fileUrl",
          "/discoveryDate"
        ]
      }
    ]
  }
}
```

### Handling Re-Processing

**Scenario**: File changes and needs to be reprocessed
- Use `discoveryDate` (not timestamp) as part of key
- Same file on different days = different discovery records
- OR: Add `fileSize` or `lastModified` to idempotency key (detect file changes)

**Configuration Option**: `AllowReprocessing` flag
- If `true`, include `fileSize` or `lastModified` in idempotency key
- If `false`, use `fileUrl` only (once per day)

---

## 7. Security and Multi-Tenancy

### Research Question
How do we enforce 100% client-scoped security trimming (SC-009)?

### Decision: Repository-Level Filtering + API Authorization

### Architecture

**Repository Pattern**:
- All repository methods accept `clientId` parameter
- Queries always filter by partition key: `WHERE c.clientId = @clientId`
- Repositories enforce: "You must know the clientId to query"

**API Layer**:
- Use ASP.NET Core Authorization
- Claims-based: `ClaimTypes.NameIdentifier` (user ID), custom claim `clientId`
- Extract `clientId` from user claims (JWT token)
- Pass `clientId` to repositories (never from request body)

**Example**:
```csharp
[Authorize]
[HttpGet("api/configurations")]
public async Task<IActionResult> GetConfigurations()
{
    // Extract clientId from authenticated user claims (not from query string)
    var clientId = User.FindFirst("clientId")?.Value 
        ?? throw new UnauthorizedAccessException("Client not assigned");
    
    var configs = await _repository.GetByClientAsync(clientId);
    return Ok(configs);
}
```

**Enforcement Points**:
1. API Layer: Authorize attribute, extract clientId from claims
2. Repository Layer: All queries include clientId filter (partition key)
3. Message Handlers: Include clientId in messages, validate against user context

**Testing**:
- Integration tests: Attempt cross-client access (should return 0 results, not error)
- Unit tests: Verify repository queries always include clientId filter

---

## 8. Error Handling and Observability

### Research Question
What logging and error handling patterns should we use for file retrieval operations?

### Decision: Structured Logging + Protocol-Specific Error Categories

### Logging Standards

**Structured Logging Fields** (per Constitution Principle V):
- `clientId` - Client owning the configuration
- `configurationId` - Configuration being executed
- `executionId` - Unique execution attempt ID
- `protocol` - FTP, HTTPS, AzureBlob
- `fileUrl` - File being checked (if applicable)
- `correlationId` - Trace across distributed calls
- `operation` - FileCheck, TokenReplacement, EventPublish

**Log Levels**:
- **Information**: Configuration created, file check started, file discovered, check completed
- **Warning**: Connection retry, file not found (if expected), token replacement warning
- **Error**: Connection failure, authentication error, configuration validation error

**Example**:
```csharp
_logger.LogInformation(
    "File check started for {ClientId}/{ConfigurationId} using {Protocol}",
    clientId, configurationId, protocol);

_logger.LogError(ex,
    "File check failed for {ClientId}/{ConfigurationId} - {ErrorCategory}",
    clientId, configurationId, "AuthenticationFailure");
```

### Error Categories

| Category | Retry | User Action |
|----------|-------|-------------|
| `AuthenticationFailure` | No | Check credentials |
| `ConnectionTimeout` | Yes (3x) | Check network/firewall |
| `FileNotFound` | No | Normal (log info, not error) |
| `InvalidConfiguration` | No | Fix configuration |
| `ProtocolError` | Yes (3x) | Contact client/provider |
| `TokenReplacementError` | No | Fix token usage |

### Metrics (Application Insights)

- `FileCheckDuration` (histogram) - Time to complete check
- `FileCheckSuccess` (counter) - Success/failure count
- `FilesDiscovered` (counter) - Number of files found
- `ProtocolErrors` (counter by category) - Error breakdown

---

## 9. Extensibility for Future Protocols

### Research Question
How do we design for extensibility to support additional protocols beyond FTP, HTTPS, Azure Blob Storage?

### Decision: Plugin Architecture via IProtocolAdapter Interface

### Architecture

**Interface Definition**:
```csharp
public interface IProtocolAdapter
{
    ProtocolType SupportedProtocol { get; }
    
    Task<IEnumerable<DiscoveredFileInfo>> CheckForFilesAsync(
        ProtocolSettings settings,
        string pathPattern,
        string filenamePattern,
        CancellationToken cancellationToken);
}

public record DiscoveredFileInfo(
    string FileUrl,
    long FileSize,
    DateTimeOffset LastModified);
```

**Factory Pattern**:
```csharp
public class ProtocolAdapterFactory
{
    private readonly Dictionary<ProtocolType, IProtocolAdapter> _adapters;
    
    public ProtocolAdapterFactory(IEnumerable<IProtocolAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(a => a.SupportedProtocol);
    }
    
    public IProtocolAdapter GetAdapter(ProtocolType protocol)
    {
        if (!_adapters.TryGetValue(protocol, out var adapter))
            throw new NotSupportedException($"Protocol {protocol} not supported");
        return adapter;
    }
}
```

### Adding New Protocols

**Steps to add a new protocol** (e.g., AWS S3):
1. Create `AwsS3ProtocolAdapter : IProtocolAdapter`
2. Implement `CheckForFilesAsync()` using AWS SDK
3. Define `AwsS3ProtocolSettings : ProtocolSettings` (bucket, region, credentials)
4. Register adapter in DI: `services.AddTransient<IProtocolAdapter, AwsS3ProtocolAdapter>()`
5. Add `ProtocolType.AwsS3` enum value
6. No changes to FileCheckService or handlers (loose coupling)

### Future Protocol Candidates

- **AWS S3**: S3 bucket file detection
- **Google Cloud Storage**: GCS bucket support
- **SFTP**: SSH-based file transfer (distinct from FTP)
- **SharePoint**: Microsoft SharePoint document libraries
- **Dropbox/Box**: Cloud storage APIs
- **Local File System**: For testing or edge scenarios

---

## Summary of Decisions

| Research Area | Decision | Rationale |
|---------------|----------|-----------|
| **Schedule Execution** | NCrontab + HostedService | Lightweight, aligns with Container Apps, no extra persistence |
| **FTP Library** | FluentFTP | Async, modern, supports FTP/FTPS/SFTP, active maintenance |
| **Partition Strategy** | `/clientId` for configs, `/clientId/configurationId` for executions | Client-scoped queries efficient, high-volume execution history isolated |
| **HTTPS Library** | HttpClient + IHttpClientFactory | Built-in, connection pooling, recommended .NET pattern |
| **Azure Blob Library** | Azure.Storage.Blobs SDK | Official SDK, Managed Identity support, async API |
| **Token Replacement** | Explicit service with validation | 100% accuracy, clear error messages, testable |
| **Idempotency** | DiscoveredFile tracking with unique key | Cosmos DB atomic operations, 100% duplicate prevention |
| **Security** | Repository-level filtering + claims-based auth | Enforce at data layer, clientId from JWT claims |
| **Observability** | Structured logging with correlation IDs | Distributed tracing, protocol-specific error categories |
| **Extensibility** | IProtocolAdapter interface + factory | Plugin architecture, add protocols without changing core logic |

---

## Next Steps

✅ **Phase 0 Complete** - All NEEDS CLARIFICATION items resolved  
➡️ **Proceed to Phase 1**: Generate data-model.md, contracts, quickstart.md

**Phase 1 Deliverables**:
1. `data-model.md` - Entities, value objects, state transitions, validation rules
2. `contracts/commands.md` - Command message contracts
3. `contracts/events.md` - Event message contracts
4. `quickstart.md` - Developer onboarding guide
5. Update agent context with new technologies
