# Implementation Plan: Manual File Check Trigger API

**Branch**: `001-file-retrieval-config` | **Date**: 2025-01-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/services/file-retrieval/specs/001-file-retrieval-config/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Add a manual trigger API endpoint to the existing File Retrieval service that allows support engineers to trigger file checks on-demand for any client configuration they have access to. The endpoint publishes the existing `ExecuteFileCheck` command with `IsManualTrigger=true`. Enhance `ExecuteFileCheckHandler` to publish a new `FileCheckTriggered` event at the start of processing to capture audit trail for both scheduled and manual executions. Maintains full client-scoped security trimming and idempotency guarantees.

## Technical Context (RiskInsure Stack)

**Language/Version**: .NET 10.0, C# 13 (nullable reference types enabled, LangVersion: latest)  
**Framework**: NServiceBus 9.x with Azure Service Bus transport  
**Messaging**: Azure Service Bus (commands via Send, events via Publish)  
**Testing**: xUnit 2.9.0 (unit/domain/application/infrastructure), Playwright (API integration via Node.js)  
**Target Platform**: Azure Container Apps (Linux containers)  
**Centralized Package Management**: Central Package Management (CPM) via Directory.Packages.props  

### Persistence Technology Decision ⚠️ REQUIRED

> **YOU MUST CHOOSE ONE** before proceeding with data model design:

- **Option A: Azure Cosmos DB (NoSQL)** - Single-partition strategy, document-based, free queries within partition
  - **When to use**: Event sourcing, high-write throughput, partition-aligned queries, flexible schema
  - **Partition key required**: Identify processing unit (e.g., `/fileRunId`, `/orderId`, `/customerId`)
  - **Cosmos Persistence**: NServiceBus.Persistence.CosmosDB 3.1.2
  
- **Option B: PostgreSQL (Relational)** - Traditional RDBMS, ACID transactions, relational integrity
  - **When to use**: Complex queries, strong relational constraints, reporting/analytics, mature tooling
  - **PostgreSQL Persistence**: NServiceBus.Persistence.Sql with PostgreSQL dialect

**DECISION**: Cosmos DB  
**Rationale**: Existing FileRetrieval service already uses Cosmos DB with `/clientId` partition key. This feature adds a single API endpoint and event—no new storage required. All queries are partition-scoped (by clientId), making Cosmos DB optimal. No change to persistence strategy needed.

### Project Structure Constraints

**Bounded Context**: File Retrieval  
**Service Location**: `services/file-retrieval/`  
**Actual Layer Structure** (concrete implementation):
- `src/FileRetrieval.Domain/` - Entities, ValueObjects, Enums, Repositories (interfaces)
- `src/FileRetrieval.Contracts/` - Commands, Events, DTOs (NServiceBus message contracts)
- `src/FileRetrieval.Application/` - MessageHandlers, Services, Protocols
- `src/FileRetrieval.Infrastructure/` - Repository implementations, Cosmos initialization, Scheduling (SchedulerHostedService)
- `src/FileRetrieval.API/` - Controllers, Models, Program.cs, Validators
- `src/FileRetrieval.Worker/` - NServiceBus endpoint host for message processing
- `test/FileRetrieval.Domain.Tests/` - xUnit domain tests
- `test/FileRetrieval.Application.Tests/` - xUnit application/handler tests
- `test/FileRetrieval.Infrastructure.Tests/` - xUnit infrastructure tests
- `test/FileRetrieval.Integration.Tests/` - Playwright API integration tests (Node.js)

**Performance Goals**: 
- API response <2 seconds (SC-001)
- Message processing <500ms p95 (handler validation + service delegation)
- No additional latency to file check execution

**Constraints**: 
- Idempotent handlers (required by Constitution IV)
- Thin message handlers delegate to Application services (Constitution VII)
- Client-scoped security trimming (existing pattern)
- Atomic state transitions with ETags (Constitution III)
- 100 concurrent file checks limit (existing semaphore in SchedulerHostedService)

**Scale/Scope**: 
- Low volume: ~100 manual triggers/day expected
- Shares concurrency pool with scheduled checks (100 limit)
- No rate limiting required (relies on existing concurrency control)

## Constitution Check (RiskInsure Principles)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Reference**: [.specify/memory/constitution.md](../../../../.specify/memory/constitution.md)

### Core Principle Compliance

- [x] **I. Domain Language Consistency** - Uses existing domain language from `file-retrieval-standards.md`: FileRetrievalConfiguration, FileCheck, ExecuteFileCheck, FileCheckTriggered. No new domain terms introduced. All terminology consistent with existing standards.

- [x] **II. Data Model Strategy** - Cosmos DB with `/clientId` partition key (existing). No new containers or partition strategies. Feature only adds API endpoint and event—no persistence changes required.

- [x] **III. Atomic State Transitions** - No entity state transitions in this feature. API validates configuration existence (read-only), sends command (stateless), returns 202. No ETags or concurrency concerns in API layer.

- [x] **IV. Idempotent Message Handlers** - Existing `ExecuteFileCheckHandler` already idempotent (checks configuration existence, generates unique ExecutionId). Enhancement adds event publishing at start—idempotency ensured via `IdempotencyKey` format: `"{ClientId}:{ConfigurationId}:triggered:{ExecutionId}"`. Multiple replays publish same event once.

- [x] **V. Structured Observability** - All logs include `CorrelationId`, `ClientId`, `ConfigurationId`. New `FileCheckTriggered` event includes same correlation fields. Existing handler already follows structured logging pattern.

- [x] **VI. Message-Based Integration** - API uses `context.Send()` for `ExecuteFileCheck` command (unicast to Worker endpoint). Handler uses `context.Publish()` for `FileCheckTriggered` event (broadcast). No cross-service HTTP calls.

- [x] **VII. Thin Message Handlers** - `ExecuteFileCheckHandler` delegates to `FileCheckService` in Application layer (existing pattern). New event publishing is 3 lines: construct event, call `context.Publish()`. No business logic in handler.

- [x] **VIII. Test Coverage Requirements** - Plan includes: API integration tests (Playwright), handler unit tests (xUnit), event publishing tests, security tests. Target: Application 80%+ (handler + service).

- [x] **IX. Technology Constraints** - Uses approved stack: .NET 10, C# 13, NServiceBus 9.x, Azure Service Bus transport, Cosmos DB, xUnit. No prohibited technologies (no EF Core, no distributed transactions).

- [x] **X. Naming Conventions** - Command: `ExecuteFileCheck` (verb+noun, already exists). Event: `FileCheckTriggered` (noun+verbPastTense). Follows naming standard.

### Violations Requiring Justification

> Fill ONLY if any principle above cannot be satisfied

*No violations. All constitutional principles satisfied.*

## Project Structure

### Documentation (this feature)

```text
services/file-retrieval/specs/001-file-retrieval-config/
├── spec.md              # Feature specification (already exists)
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── FileCheckTriggered.md  # Event contract specification
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (File Retrieval Service - Existing Structure)

**Service Root**: `services/file-retrieval/`

```text
services/file-retrieval/
├── src/
│   ├── FileRetrieval.API/
│   │   ├── Controllers/
│   │   │   ├── ConfigurationController.cs      # ✨ ADD: TriggerFileCheck endpoint
│   │   │   └── ExecutionHistoryController.cs   # (existing, no changes)
│   │   ├── Models/
│   │   │   ├── TriggerFileCheckResponse.cs     # ✨ NEW response DTO
│   │   │   └── (existing models...)
│   │   ├── Program.cs                          # (existing, no changes)
│   │   └── FileRetrieval.API.csproj
│   ├── FileRetrieval.Contracts/
│   │   ├── Commands/
│   │   │   └── ExecuteFileCheck.cs             # (existing, already has IsManualTrigger)
│   │   ├── Events/
│   │   │   ├── FileCheckTriggered.cs           # ✨ NEW event contract
│   │   │   ├── FileCheckCompleted.cs           # (existing)
│   │   │   └── FileCheckFailed.cs              # (existing)
│   │   └── FileRetrieval.Contracts.csproj
│   ├── FileRetrieval.Application/
│   │   ├── MessageHandlers/
│   │   │   └── ExecuteFileCheckHandler.cs      # ✨ MODIFY: Publish FileCheckTriggered
│   │   └── Services/
│   │       └── FileCheckService.cs             # (existing, no changes)
│   ├── FileRetrieval.Domain/
│   │   ├── Entities/
│   │   │   └── FileRetrievalConfiguration.cs   # (existing, no changes)
│   │   └── Repositories/
│   │       └── IFileRetrievalConfigurationRepository.cs  # (existing, no changes)
│   ├── FileRetrieval.Infrastructure/
│   │   └── Repositories/
│   │       └── FileRetrievalConfigurationRepository.cs   # (existing, no changes)
│   └── FileRetrieval.Worker/
│       ├── Program.cs                          # (existing, NServiceBus host)
│       └── FileRetrieval.Worker.csproj
├── test/
│   ├── FileRetrieval.Application.Tests/
│   │   └── MessageHandlers/
│   │       └── ExecuteFileCheckHandlerTests.cs # ✨ ADD: Event publishing tests
│   ├── FileRetrieval.Integration.Tests/
│   │   └── Controllers/
│   │       └── ConfigurationController.TriggerTests.cs  # ✨ NEW integration tests
│   └── (other test projects...)
└── docs/
    └── file-retrieval-standards.md             # (existing, no changes)
```

**Key Changes Summary**:
- ✨ 1 new API endpoint in existing `ConfigurationController`
- ✨ 1 new event contract: `FileCheckTriggered`
- ✨ 1 new response DTO: `TriggerFileCheckResponse`
- ✨ Modify existing handler to publish event
- ✨ Tests for new endpoint and event publishing

**No new projects or major refactoring required.**

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

*No complexity violations. This is a straightforward enhancement:*
- Adds 1 API endpoint to existing controller (standard REST pattern)
- Adds 1 event contract (follows existing event patterns)
- Modifies 1 handler to publish event (3-line addition)
- Uses existing security, validation, and message infrastructure
- No new projects, patterns, or architectural deviations

**Complexity Score**: ⭐ Low (incremental enhancement to mature codebase)

---

## Phase 0: Research Findings

**Status**: ✅ Complete  
**Document**: [research.md](research.md)

### Key Decisions Made

**R1 - Security Pattern**: Reuse existing `GetUserIdFromClaims()` helper method to extract user identity from JWT for audit trail. Priority: `ClaimTypes.NameIdentifier` → `"sub"` → `ClaimTypes.Email` → `"unknown-user"`.

**R2 - Event Timing**: Publish `FileCheckTriggered` event immediately after loading configuration and validating IsActive, **before** calling `FileCheckService.ExecuteCheckAsync()`. This captures intention to execute, not result (separate events for completion/failure).

**R3 - Idempotency Strategy**: Generate `ExecutionId` in handler before any processing. Pass to service as parameter (requires service signature change). Use ExecutionId in IdempotencyKey format: `"{ClientId}:{ConfigurationId}:triggered:{ExecutionId}"`. This ensures stable key across handler retries.

**R4 - API Response Model**: Create `TriggerFileCheckResponse` with 4 fields: ConfigurationId, ExecutionId, TriggeredAt, Message. Provides tracking ID for support engineers to monitor execution progress via existing ExecutionHistory API.

**R5 - Validation Strategy**: Two-step validation in API before sending command:
1. Call `ConfigurationService.GetByIdAsync(clientId, configId)` - Returns null if not found OR wrong client (security trimming)
2. Check `configuration.IsActive` - Return 400 Bad Request if false

Error codes: 404 (not found OR wrong client), 400 (inactive), 500 (messaging error).

**R6 - User Identity Flow**: Add optional `TriggeredBy` field to `ExecuteFileCheck` command. API populates with user ID from JWT. Handler defaults to "Scheduler" if null. Backward compatible (existing scheduled messages omit field).

**R7 - ExecutionId Generation**: Generate in handler (not API, not service) to ensure stable ID across handler retries while allowing separate executions for separate API requests. Requires `FileCheckService.ExecuteCheckAsync` signature change to accept `executionId` parameter.

### Technologies & Patterns Confirmed

- ✅ **Authentication**: JWT with clientId claim (existing pattern)
- ✅ **Authorization**: ClientAccess policy with claim requirement
- ✅ **Security Trimming**: Repository-level filtering by clientId partition key
- ✅ **Command Sending**: NServiceBus `IMessageSession.Send()`
- ✅ **Event Publishing**: NServiceBus `IMessageHandlerContext.Publish()`
- ✅ **Idempotency**: ExecutionId-based IdempotencyKey with outbox deduplication
- ✅ **Error Handling**: Try/catch with structured logging and appropriate HTTP status codes

### Unknowns Resolved

✅ How to ensure idempotent event publishing → Generate ExecutionId in handler before service call  
✅ How to track manual vs scheduled triggers → Use existing IsManualTrigger field + new TriggeredBy field  
✅ What validation before sending command → Check existence + active status in API  
✅ How to extract user identity → Reuse existing GetUserIdFromClaims() helper  
✅ What error codes for different failure modes → 404 (not found/wrong client), 400 (inactive), 500 (system error)  
✅ Where to publish FileCheckTriggered → In handler after loading config, before file check execution  

**No blockers. All patterns exist in current codebase. Ready for Phase 1 design.**

---

## Phase 1: Design & Contracts

**Status**: ✅ Complete  
**Documents**: [data-model.md](data-model.md), [contracts/FileCheckTriggered.md](contracts/FileCheckTriggered.md), [quickstart.md](quickstart.md)

### Data Model Summary

**No database changes required.** This feature operates entirely at the application and API layers.

**Existing Entities (Unchanged)**:
- `FileRetrievalConfiguration` - Read-only access for validation
- `FileRetrievalExecution` - Created by service (existing flow)
- `DiscoveredFile` - Created by service (existing flow)

**Modified Command**: `ExecuteFileCheck`
- ✨ Added: `string? TriggeredBy` field (nullable, optional)
- Purpose: Track user identity for manual triggers
- Backward compatible: Scheduled executions omit field (null)

**New Event**: `FileCheckTriggered`
- Purpose: Audit trail for all file check initiations
- Published: Before file check execution begins
- Fields: ClientId, ConfigurationId, ConfigurationName, Protocol, ExecutionId, ScheduledExecutionTime, IsManualTrigger, TriggeredBy
- IdempotencyKey: `"{ClientId}:{ConfigurationId}:triggered:{ExecutionId}"`
- Subscribers: Audit logs, monitoring dashboards, analytics systems

**New API Response**: `TriggerFileCheckResponse`
- Fields: ConfigurationId, ExecutionId, TriggeredAt, Message
- Purpose: Confirm acceptance and provide tracking ID

### Service Signature Change

**FileCheckService.ExecuteCheckAsync**:
- Old: `Task<FileCheckResult> ExecuteCheckAsync(FileRetrievalConfiguration config, DateTimeOffset scheduledTime, CancellationToken ct)`
- New: `Task<FileCheckResult> ExecuteCheckAsync(FileRetrievalConfiguration config, DateTimeOffset scheduledTime, Guid executionId, CancellationToken ct)`
- Impact: Handler and tests must pass ExecutionId parameter

### Integration Points

**API Layer**:
- `ConfigurationService.GetByIdAsync()` - Validate configuration existence and ownership
- `IMessageSession.Send()` - Send ExecuteFileCheck command

**Application Layer**:
- `ExecuteFileCheckHandler` - Publish FileCheckTriggered event before processing
- `FileCheckService` - Accept executionId parameter (signature change)

**No new services or repositories required.**

### Performance Expectations

**API Response Time**: <2 seconds (SC-001)
- JWT claim extraction: <1ms
- Configuration query: ~5-10ms (point read)
- IsActive check: <1ms
- Command send: ~10-50ms
- **Total**: ~20-70ms (well under target)

**Handler Processing Addition**: ~10-20ms (event publishing)
- Negligible impact (handler already publishes 1-2 events)

### Security Model

**Multi-Tenancy**:
- Partition key: `/clientId` (existing)
- All queries scoped by clientId (automatic filtering)

**JWT Claims**:
- `clientId` - Required, identifies client
- `sub`/`NameIdentifier`/`Email` - User identity (fallback priority)

**Authorization Flow**:
1. Middleware validates JWT signature
2. Policy "ClientAccess" requires clientId claim
3. Controller extracts clientId (never from request body)
4. Repository filters by partition key
5. 404 response doesn't leak existence for wrong client

### Contract Specifications

**Event**: `FileCheckTriggered` v1.0
- 12 fields (4 metadata, 4 context, 2 tracking, 2 trigger)
- Published by: `ExecuteFileCheckHandler`
- Subscribers: Audit, monitoring, analytics (to be implemented)
- Delivery: At-least-once with outbox deduplication
- Size: ~500 bytes

**API Endpoint**: `POST /api/configuration/{configurationId}/trigger`
- Auth: JWT with clientId claim
- Request: No body (configurationId in URL)
- Response: 202 Accepted with TriggerFileCheckResponse
- Errors: 400 (inactive), 404 (not found/wrong client), 401 (auth), 500 (system)

**Full specifications in**:
- [data-model.md](data-model.md) - Entity and DTO structures
- [contracts/FileCheckTriggered.md](contracts/FileCheckTriggered.md) - Event contract details

---

## Phase 2: Implementation Tasks

**Status**: ⏸️ Not started (run `/speckit.tasks` command to generate)

**Expected Task Breakdown**:
1. **Contracts** (2 tasks):
   - Create `FileCheckTriggered` event contract
   - Add `TriggeredBy` field to `ExecuteFileCheck` command

2. **API Layer** (3 tasks):
   - Add `TriggerFileCheckResponse` DTO
   - Add `TriggerFileCheck` endpoint to `ConfigurationController`
   - Add integration tests for trigger endpoint

3. **Application Layer** (3 tasks):
   - Modify `ExecuteFileCheckHandler` to publish `FileCheckTriggered` event
   - Update `FileCheckService.ExecuteCheckAsync` signature to accept `executionId`
   - Add handler unit tests for event publishing

4. **Documentation** (2 tasks):
   - Update OpenAPI/Swagger documentation
   - Update file-retrieval-standards.md with manual trigger pattern

**Estimated Total**: ~10 tasks, 6-8 hours implementation + testing

**Command to generate detailed tasks**: `/speckit.tasks`

---

## Dependencies & Ordering

### Critical Path

```text
1. Add TriggeredBy to ExecuteFileCheck command (enables user tracking)
   ↓
2. Create FileCheckTriggered event contract (defines event schema)
   ↓
3. Modify ExecuteFileCheckHandler to publish event (implements audit trail)
   ↓
4. Update FileCheckService signature (enables ExecutionId passing)
   ↓
5. Add API endpoint (exposes manual trigger capability)
   ↓
6. Add tests (verify functionality)
```

### Parallel Work Opportunities

**After Step 2 (event contract created)**:
- API endpoint implementation (Step 5)
- Handler modification (Step 3)
- Service signature update (Step 4)

These can proceed in parallel once contracts are defined.

### External Dependencies

**None.** All dependencies already exist:
- ExecuteFileCheck command ✓
- ExecuteFileCheckHandler ✓
- FileCheckService ✓
- ConfigurationController ✓
- Security infrastructure ✓
- Test projects ✓

---

## Risk Assessment

### Low Risk ✅

**Why This Feature is Low Risk**:
- ✨ Small scope: 1 endpoint, 1 event, 1 handler modification
- ✨ No database changes: Uses existing entities and queries
- ✨ No new projects: All changes in existing layers
- ✨ Backward compatible: Command change is additive (nullable field)
- ✨ Established patterns: Reuses proven security and messaging patterns
- ✨ Independent: Doesn't affect scheduled execution flow
- ✨ Testable: Clear test scenarios at each layer

**Potential Issues & Mitigations**:

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Service signature change breaks callers | Low | Medium | Only 2 callers (handler + tests), both updated in same PR |
| Event publishing increases handler latency | Low | Low | ~10-20ms addition (negligible vs file check duration) |
| Concurrent manual + scheduled triggers | Low | Low | Existing concurrency semaphore (100 limit) handles both |
| Missing clientId claim in JWT | Low | Medium | Existing GetClientIdFromClaims() throws clear exception |

---

## Deployment Plan

### Deployment Order

**Step 1**: Deploy Worker (FileRetrieval.Worker)
- Contains updated handler with event publishing
- Contains updated service with new signature
- Can process both old and new commands (backward compatible)

**Step 2**: Deploy API (FileRetrieval.API)
- Contains new trigger endpoint
- Sends commands with TriggeredBy field populated

**Rollback**:
- Revert API deployment → Removes trigger endpoint
- Event publishing in handler is harmless (no subscribers yet)
- Can rollback independently without data loss

**Downtime**: None (backward compatible changes)

---

## Testing Strategy Summary

### Test Coverage Targets

**Application Layer**: 80%+ (Constitution VIII)
- Handler event publishing logic
- Service executionId parameter handling

**API Layer**: Integration tests for all scenarios
- Happy path (202 Accepted)
- Error scenarios (400, 404)
- Security trimming (403 implicit as 404)

### Test Pyramid

```text
┌─────────────────────────────────────┐
│  Integration Tests (5-6 scenarios)  │  ← API endpoint behavior
├─────────────────────────────────────┤
│    Handler Unit Tests (4-5)         │  ← Event publishing logic
├─────────────────────────────────────┤
│    Service Unit Tests (1-2)         │  ← ExecutionId parameter usage
└─────────────────────────────────────┘
```

**Total Test Scenarios**: ~12 tests

**Test Files**:
- `test/FileRetrieval.Integration.Tests/Controllers/ConfigurationController.TriggerTests.cs`
- `test/FileRetrieval.Application.Tests/MessageHandlers/ExecuteFileCheckHandlerTests.cs` (extend existing)
- `test/FileRetrieval.Application.Tests/Services/FileCheckServiceTests.cs` (extend existing)

---

## Success Metrics (SC from spec.md)

**SC-001**: Support engineers can trigger file checks via API with <2s response
- ✅ **Measurement**: API response time metric (target: <2s, expected: <100ms)
- ✅ **Test**: Integration test verifies 202 response within reasonable time

**SC-002**: Security trimming prevents cross-client access (100% compliance)
- ✅ **Measurement**: All trigger attempts logged with authorization result
- ✅ **Test**: Integration test verifies 404 for wrong client (not 403 - no info disclosure)

**SC-003**: Every file check publishes FileCheckTriggered event before processing
- ✅ **Measurement**: Event publishing metric matches handler invocation count
- ✅ **Test**: Handler unit test verifies event published before service call

**SC-004**: Manual triggers integrate with concurrency controls (100 limit maintained)
- ✅ **Measurement**: Existing semaphore in SchedulerHostedService applies to both trigger sources
- ✅ **Test**: No new test needed (existing concurrency mechanism unchanged)

**SC-005**: Monitoring can differentiate manual vs scheduled executions
- ✅ **Measurement**: Event contains IsManualTrigger and TriggeredBy fields
- ✅ **Test**: Handler test verifies fields populated correctly for both trigger types

---

## Operational Readiness

### Monitoring

**Application Insights Metrics**:
- `FileRetrieval.ManualTrigger.Count` - Total manual triggers
- `FileRetrieval.ManualTrigger.ByUser` - Per-user trigger frequency
- `FileRetrieval.FileCheckTriggered.Count` - All triggers (manual + scheduled)

**KQL Queries** (for dashboards):
```kusto
// Manual trigger frequency by day
customEvents
| where name == "FileCheckTriggered"
| where customDimensions.IsManualTrigger == "true"
| summarize count() by bin(timestamp, 1d)

// Top support engineers by triggers
customEvents
| where name == "FileCheckTriggered"
| where customDimensions.IsManualTrigger == "true"
| summarize TriggerCount = count() by tostring(customDimensions.TriggeredBy)
| order by TriggerCount desc
```

### Logging

**New Log Statements** (4 locations):
1. API endpoint start: "Triggering file check for configuration {ConfigurationId}"
2. API validation failure: "Configuration {ConfigurationId} not found/inactive"
3. Handler event publishing: "Publishing FileCheckTriggered event (ExecutionId: {ExecutionId})"
4. API success: "File check triggered successfully (ExecutionId: {ExecutionId})"

**Log Levels**:
- Information: Successful triggers and event publishing
- Warning: Validation failures (not found, inactive)
- Error: Messaging failures or unexpected exceptions

### Alerting

**Recommended Alerts**:
- Manual trigger rate spikes (>50/hour may indicate issue)
- Trigger failures (>5% error rate)
- Missing clientId claims (unauthorized access attempts)

---

## Implementation Effort Estimate

### Breakdown by Component

| Component | Files | Lines of Code | Effort |
|-----------|-------|---------------|--------|
| Event Contract | 1 new | ~50 | 30 min |
| Command Modification | 1 modified | +5 | 15 min |
| API Response DTO | 1 new | ~30 | 15 min |
| API Endpoint | 1 modified | +60 | 1 hour |
| Handler Modification | 1 modified | +25 | 45 min |
| Service Signature | 1 modified | +5 | 30 min |
| Integration Tests | 1 new | ~150 | 2 hours |
| Unit Tests | 2 modified | ~100 | 1.5 hours |
| Documentation | Swagger update | ~50 | 30 min |

**Total Estimated Effort**: 6.5-8 hours (including testing and documentation)

**Complexity**: ⭐ Low  
**Risk**: ⚠️ Low  
**Dependencies**: None (all infrastructure exists)

---

## Definition of Done (from spec.md)

- [ ] All acceptance criteria have passing tests (xUnit unit + integration tests)
- [ ] FileCheckTriggered event includes correlation IDs in all log statements
- [ ] Idempotency verified (duplicate FileCheckTriggered event test passes)
- [ ] Domain test coverage ≥90%, handler coverage ≥80%
- [ ] API endpoint follows existing security pattern (JWT claims extraction)
- [ ] OpenAPI/Swagger documentation updated for new endpoint
- [ ] PR approved, merged to main

---

## Next Steps

**To proceed with implementation**:

1. **Generate Tasks**: Run `/speckit.tasks` command to create `tasks.md` with detailed, ordered implementation tasks
2. **Create Feature Branch**: `git checkout -b 001-file-retrieval-config`
3. **Implement Tasks**: Follow task order in tasks.md
4. **Test Continuously**: Run tests after each task group
5. **Update Documentation**: Keep swagger/standards docs in sync
6. **PR Review**: Verify constitution compliance and test coverage

**Command**: `/speckit.tasks` (generates tasks.md from this plan)

---

**Plan Status**: ✅ Complete - Ready for task generation and implementation

