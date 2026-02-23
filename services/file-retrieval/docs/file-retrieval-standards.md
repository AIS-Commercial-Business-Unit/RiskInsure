# File Retrieval Domain Standards

**T133 - Principle I**: Domain Language Consistency

This document defines the domain terminology, coding standards, and architectural patterns for the File Retrieval bounded context within the RiskInsure platform.

## Domain Glossary

### Core Concepts

**FileRetrievalConfiguration**
- A configured rule that defines WHERE to check for files, WHEN to check, and WHAT TO DO when files are found
- Contains protocol settings, file patterns with date tokens, schedule definition, and workflow integration actions
- Scoped to a single client for multi-tenant isolation
- Uniquely identified by `ConfigurationId` (Guid)

**Protocol**
- The communication method used to access file locations: FTP, HTTPS, or Azure Blob Storage
- Each protocol has specific settings (server, credentials, connection parameters)
- Protocol adapters handle protocol-specific communication logic

**Schedule**
- Defines when file checks should execute using cron expressions and timezone
- Evaluated by the scheduler service to trigger checks at appropriate times
- Examples: Daily at 2 AM, every hour, first day of month

**Token**
- Placeholder in file paths/names replaced with date values at execution time
- Format: `{yyyy}`, `{mm}`, `{dd}`, `{yy}`, `{mmm}`, `{mmmm}`
- Example: `/files/{yyyy}/{mm}/{dd}/data_{yyyy}{mm}{dd}.xlsx`
- Enables date-based file discovery without hardcoding dates

**FileCheck**
- Single execution of a FileRetrievalConfiguration
- Connects to configured location, evaluates patterns with current date, checks for matching files
- Results in FileRetrievalExecution record (success/failure, files found, duration)

**DiscoveredFile**
- Record of a file detected during a file check
- Includes file URL, size, last modified date, discovery timestamp
- Triggers configured events and commands to workflow platform
- Idempotency ensured via unique key (clientId + configurationId + fileUrl + discoveryDate)

**FileRetrievalExecution**
- Historical record of a file check execution
- Tracks status (Pending, Running, Completed, Failed), duration, files found, events published
- Includes error details and categorization for failed executions
- Retained for 90 days (TTL) for audit and monitoring

### Workflow Integration

**EventDefinition**
- Specification of an event to publish when files are discovered
- Contains event type, destination, and event data template
- Published to Azure Service Bus for workflow orchestration platform

**CommandDefinition**
- Specification of a command to send when files are discovered
- Contains command type, target endpoint, and command data
- Sent via NServiceBus to specific workflow handlers

**ConfigurationExecution**
- The process of scheduler triggering a file check based on schedule evaluation
- Sends `ExecuteFileCheck` command via message bus
- Ensures scheduled checks execute within 1 minute of scheduled time (SC-002)

### States and Status

**ExecutionStatus**
- `Pending`: Execution queued but not started
- `Running`: File check actively in progress
- `Completed`: Successfully completed (files found or not found)
- `Failed`: Execution failed due to error (auth, connection, protocol)

**DiscoveryStatus**
- `Discovered`: File found and recorded
- `Processed`: Events/commands successfully published
- `Failed`: Error publishing events/commands

**ConfigurationActive**
- `IsActive = true`: Configuration participates in scheduled checks
- `IsActive = false`: Soft-deleted configuration, excluded from scheduler

### Error Categories

**AuthenticationFailure**
- Credential validation failed (wrong password, expired token, invalid key)

**ConnectionTimeout**
- Unable to establish connection within timeout period
- Network unreachable or server not responding

**ProtocolError**
- Protocol-specific error (FTP 5xx, HTTP 4xx/5xx, Azure storage exception)

**FileNotFound**
- No files match the configured pattern at execution time (not an error, valid outcome)

**PermissionDenied**
- Authenticated but lacks permission to access location or list files

## Coding Standards

### Naming Conventions

**Entities**
- PascalCase: `FileRetrievalConfiguration`, `FileRetrievalExecution`, `DiscoveredFile`
- Singular nouns: Entity represents single instance
- Suffix with purpose: `*Configuration`, `*Execution`, `*Definition`

**Value Objects**
- PascalCase: `ProtocolSettings`, `ScheduleDefinition`, `EventDefinition`, `FilePattern`
- Immutable: All properties `init` or `readonly`
- Validation in constructor

**Enums**
- PascalCase type: `ProtocolType`, `ExecutionStatus`, `AuthType`
- PascalCase values: `FTP`, `HTTPS`, `AzureBlob` (not `Ftp`, `Https`)
- Explicit values for serialization stability

**Commands/Events**
- PascalCase imperative verbs (commands): `CreateConfiguration`, `ExecuteFileCheck`, `DeleteConfiguration`
- PascalCase past tense (events): `ConfigurationCreated`, `FileCheckCompleted`, `FileDiscovered`
- Suffix: `*Command` (optional), `*Event` (optional, use context)

**Repositories**
- Interface: `I{EntityName}Repository` → `IFileRetrievalConfigurationRepository`
- Implementation: `{EntityName}Repository` → `FileRetrievalConfigurationRepository`
- Methods: `{Action}Async` → `GetByIdAsync`, `CreateAsync`, `UpdateAsync`

### Async/Await Patterns

```csharp
// ✅ Good: Async all the way
public async Task<FileRetrievalConfiguration> CreateAsync(
    FileRetrievalConfiguration configuration,
    CancellationToken cancellationToken = default)
{
    configuration.Validate();
    var created = await _repository.CreateAsync(configuration, cancellationToken);
    return created;
}

// ❌ Bad: Blocking on async
public FileRetrievalConfiguration Create(FileRetrievalConfiguration configuration)
{
    return _repository.CreateAsync(configuration).Result; // Deadlock risk!
}
```

### Structured Logging

```csharp
// ✅ Good: Structured with context
_logger.LogInformation(
    "Successfully created configuration {ConfigurationId} for client {ClientId}",
    configuration.Id,
    configuration.ClientId);

// ❌ Bad: String interpolation
_logger.LogInformation($"Created config {configuration.Id}");
```

### Error Handling

```csharp
// ✅ Good: Specific exceptions, context, re-throw for NServiceBus retry
try
{
    await _service.ExecuteAsync(cancellationToken);
}
catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
{
    _logger.LogWarning(ex, "ETag conflict for configuration {ConfigurationId}", configId);
    throw new InvalidOperationException("Configuration was modified by another request", ex);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to execute file check for configuration {ConfigurationId}", configId);
    throw; // Let NServiceBus handle retry
}
```

## Architectural Patterns

### Repository Pattern
- All data access through repository interfaces defined in Domain layer
- Implementations in Infrastructure layer (Cosmos DB)
- Client-scoped access (partition key = clientId) enforced at repository level

### Message-Based Integration
- All cross-boundary communication via Azure Service Bus / NServiceBus
- Commands: Direct actions (`CreateConfiguration`, `ExecuteFileCheck`)
- Events: Notifications (`ConfigurationCreated`, `FileDiscovered`)
- Idempotency: Every message has `IdempotencyKey` for duplicate detection

### Multi-Tenancy
- Partition key: `clientId` for all entities
- Security: JWT claims extraction enforces client isolation in API controllers
- Queries: Always scoped by `clientId` (single-partition queries per Principle II)

### Optimistic Concurrency
- ETags for configuration updates (Cosmos DB)
- Conflict detection returns 409 Conflict with latest ETag
- Client must retrieve latest version and retry

### Value Objects
- Immutable data structures with validation
- No identity - compared by value equality
- Examples: `ProtocolSettings`, `ScheduleDefinition`, `EventDefinition`

### Domain Events
- Published after state changes (configuration created/updated/deleted)
- Enable eventual consistency and decoupled workflows
- Correlation IDs propagate across event chains

## Security Standards

### Credential Management
- **Never** store plain-text passwords/secrets in code or configuration
- All credentials reference Azure Key Vault secrets: `{SecretName}`
- Protocol adapters retrieve secrets from Key Vault at runtime
- Sanitize secrets in API responses: `[REDACTED]`

### Client Isolation
- Extract `clientId` from JWT claims (not request body)
- Repository methods require `clientId` parameter
- Cosmos DB partition isolation prevents cross-client data access

### Authorization
- API requires JWT authentication: `[Authorize(Policy = "ClientAccess")]`
- Worker service uses Managed Identity for Azure resources
- Least-privilege principle: Grant only required permissions

## Performance Standards

### Success Criteria (from spec)
- **SC-002**: Scheduled checks execute within 1 minute of scheduled time (99%)
- **SC-003**: File discovery to event publish latency < 5 seconds
- **SC-004**: Support 100 concurrent file checks without degradation
- **SC-007**: Zero duplicate workflow triggers (idempotency)
- **SC-008**: 100% accurate date token replacement
- **SC-009**: 100% client-scoped security trimming enforcement

### Query Optimization
- Use composite indexes for common filter combinations
- Single-partition queries only (no cross-partition scans)
- Pagination for large result sets (50-100 items per page)

### Concurrency Control
- Semaphore limits concurrent file checks to 100 (SC-004)
- Distributed locking prevents duplicate scheduled checks
- Graceful degradation: One configuration failure doesn't affect others

## Testing Standards

### Unit Tests
- AAA pattern: Arrange, Act, Assert
- Test naming: `MethodName_Scenario_ExpectedBehavior`
- Mock external dependencies (repositories, message bus, Key Vault)
- Target coverage: 90%+ for Domain, 80%+ for Application

### Integration Tests
- Test against real Cosmos DB (emulator or dev instance)
- Test protocol adapters with test servers (FTP, mock HTTP, Azurite)
- Verify message publishing and handling
- Test multi-tenant isolation

### Contract Tests
- Verify command/event schemas don't break consumers
- Validate API request/response contracts
- Test backward compatibility for versioned APIs

## Monitoring Standards

### Metrics Collection
- Custom metrics via `FileRetrievalMetricsService`
- Track: configurations created/deleted, executions, files discovered, failures
- Group by: clientId, protocol, status
- Export to Application Insights

### Structured Logging
- Always include: `ConfigurationId`, `ClientId`, `CorrelationId`, `ExecutionId`
- Use log levels appropriately:
  - `Debug`: Detailed flow for troubleshooting
  - `Information`: Key state transitions, metrics
  - `Warning`: Recoverable errors, degraded state
  - `Error`: Failures requiring intervention

### Distributed Tracing
- Correlation IDs propagate across messages and services
- Application Insights tracks end-to-end request flows
- Enable cross-service trace correlation

## References

- **Constitution**: `/.specify/constitution.md` - Architectural principles
- **Data Model**: `/specs/001-file-retrieval-config/data-model.md`
- **Contracts**: `/specs/001-file-retrieval-config/contracts/`
- **Deployment**: `/services/file-retrieval/docs/deployment.md`
- **Monitoring**: `/services/file-retrieval/docs/monitoring.md`
- **Runbook**: `/services/file-retrieval/docs/runbook.md`

---

**Last Updated**: 2025-01-24  
**Owner**: Platform Engineering Team
