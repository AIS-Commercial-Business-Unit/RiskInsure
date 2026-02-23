# Feature Specification: Manual File Check Trigger API (Quick)

**Feature Branch**: `001-file-retrieval-config`  
**Created**: 2025-01-24  
**Status**: Draft  
**Input**: User description: "I'd like to add an API endpoint to FileRetrieval.API that will trigger the ExecuteFileCheck command for an existing configuration. It should be security trimmed so that a client can only trigger the ExecuteFileCheck command for that client's configurations. Also, any time the ExecuteFileCheck command is issued, we should also trigger an event showing that the command was issued."

> **Quick Spec Template**: Use this when domain docs already exist. Focus on scenarios, messages, and acceptance criteria only. Reference existing docs for patterns/standards.

---

## Context (References)

**Bounded Context**: File Retrieval  
**Domain Docs**: [services/file-retrieval/docs/file-retrieval-standards.md](../../docs/file-retrieval-standards.md)  
**Applies Rules**: All general patterns (multi-tenancy, security trimming, idempotency, message-based integration)

**Constitution**: [.specify/memory/constitution.md](../../../../.specify/memory/constitution.md)  
**Project Structure**: [copilot-instructions/project-structure.md](../../../../copilot-instructions/project-structure.md)  
**Messaging Patterns**: [copilot-instructions/messaging-patterns.md](../../../../copilot-instructions/messaging-patterns.md)

**Related**: This enhances the existing FileRetrieval feature documented in [IMPLEMENTATION-SUMMARY.md](../../IMPLEMENTATION-SUMMARY.md)

---

## What's New (The Delta)

*This specification adds manual triggering capability to the existing scheduled file check system.*

### Primary Scenario: Support Engineer Manually Triggers File Check

**As a** support engineer helping a client troubleshoot file integration issues  
**I want to** manually trigger a file check for a specific configuration without waiting for the next scheduled run  
**So that** I can immediately verify whether files are present and diagnose integration problems in real-time

**Acceptance Criteria**:
1. **Given** a client has an active file retrieval configuration, **When** a support engineer calls the trigger API with valid clientId and configurationId from their JWT, **Then** the system sends an ExecuteFileCheck command with IsManualTrigger=true and returns 202 Accepted immediately
2. **Given** a support engineer attempts to trigger a file check for another client's configuration, **When** the API validates security, **Then** the system returns 403 Forbidden without sending any command
3. **Given** a support engineer attempts to trigger a file check for a non-existent or inactive configuration, **When** the API validates the configuration, **Then** the system returns 404 Not Found or 400 Bad Request without sending any command
4. **Given** any ExecuteFileCheck command is issued (scheduled or manual), **When** the handler begins processing, **Then** the system publishes a FileCheckTriggered event before executing the file check
5. **Given** multiple support engineers trigger the same configuration simultaneously, **When** commands are processed, **Then** each execution is tracked independently with unique ExecutionIds and correlation IDs

---

### Additional Scenarios

**Scenario 2: Monitoring System Tracks Manual vs Scheduled Checks**
- **Given** a FileCheckTriggered event is published, **When** monitoring systems consume the event, **Then** they can distinguish manual triggers from scheduled executions for audit and analytics purposes

**Scenario 3: Client Self-Service Trigger (Future Enhancement Preparation)**
- **Given** the API endpoint exists with security trimming, **When** we extend authorization policies in the future, **Then** clients can trigger their own checks without requiring support engineer access (requires only policy change, no API changes)

---

### Edge Cases & Failure Modes

- **What if** a configuration is triggered manually while a scheduled execution is already in progress?
  - **Answer**: Both execute independently with separate ExecutionIds. Concurrency semaphore (100 limit) prevents system overload. Idempotency in file discovery prevents duplicate workflow triggers.

- **What if** the ExecuteFileCheckHandler fails after receiving the command but before publishing FileCheckTriggered?
  - **Answer**: NServiceBus retry policy will retry the handler. FileCheckTriggered event will be published on first successful handling. Idempotency ensures no duplicate events.

- **What if** a support engineer's JWT token lacks the clientId claim?
  - **Answer**: GetClientIdFromClaims() throws UnauthorizedAccessException, API returns 401 Unauthorized before sending any command.

- **What if** the configuration exists but IsActive=false (soft-deleted)?
  - **Answer**: API should return 404 Not Found to prevent triggering checks on disabled configurations. Handler already skips inactive configurations.

- **What if** the message bus is unavailable when the API tries to send the command?
  - **Answer**: Send operation throws exception, API returns 500 Internal Server Error. Client can retry. No partial state created (command-query separation ensures consistency).

---

## Message Contracts (New/Changed)

### Events (New)

**Event Name**: FileCheckTriggered

**Purpose**: Notify subscribers that a file check has been initiated, capturing whether it was manually triggered or scheduled.

**When Published**: Immediately when ExecuteFileCheck command is received and validated, before file check execution begins.

**Key Information Captured**:
- Client identifier and configuration identifier
- Configuration name and protocol type
- Whether trigger was manual or scheduled
- Who/what triggered the check (user identifier or "Scheduler")
- Scheduled execution time
- Unique execution identifier for tracking
- Standard message metadata (correlation ID, timestamp, idempotency key)

**Subscribers**: Audit log systems, monitoring dashboards, analytics systems

**Idempotency**: Each file check execution generates exactly one FileCheckTriggered event, even if messages are replayed.

---

### Commands (Modified - existing ExecuteFileCheck)

The existing ExecuteFileCheck command already supports the IsManualTrigger flag. No structural changes needed.

**API Usage**: When sending command from the new API endpoint, IsManualTrigger will be set to true to distinguish from scheduler-initiated checks.

---

### API Endpoints (New)

**Endpoint**: `POST /api/configuration/{configurationId}/trigger`

**Authentication**: Requires valid JWT token with client identifier claim

**Request**:
- Configuration identifier in URL path
- Client identifier extracted from JWT token
- No request body required

**Successful Response** (202 Accepted):
- Configuration identifier confirmed
- Execution identifier for tracking
- Trigger timestamp
- Confirmation message

**Error Responses**:
- **400 Bad Request**: Configuration exists but is inactive/disabled
- **403 Forbidden**: Configuration belongs to different client (security trimming enforced)
- **404 Not Found**: Configuration does not exist
- **500 Internal Server Error**: System error during command sending

**Purpose**: Allow manual triggering of file checks for existing configurations with client-scoped security.

**Security**: 
- Client identifier extracted from authenticated user's JWT claims only
- Configuration must belong to requesting client
- No cross-client access permitted
- Configuration existence and status validated before sending command

**Response Behavior**: 
- Returns immediately after sending command (asynchronous pattern)
- Does not wait for file check completion
- Provides execution identifier for status tracking via existing monitoring endpoints

---

## Data Changes

**Persistence**: Cosmos DB (existing FileRetrievalConfiguration container)

- **Partition Key**: `/clientId` (no changes - existing pattern)
- **Document Types**: No new document types - uses existing `FileRetrievalConfiguration` and `FileRetrievalExecution`
- **State Transitions**: No entity state changes - API reads configuration, sends command, returns immediately

### Idempotency Strategy:

**For FileCheckTriggered event**:
- IdempotencyKey format: `"{ClientId}:{ConfigurationId}:triggered:{ExecutionId}"`
- ExecutionId is unique per file check execution (generated in handler)
- Ensures exactly one FileCheckTriggered event per execution, even if message is replayed

**For ExecuteFileCheck command (existing)**:
- Already has IdempotencyKey in command: `"{clientId}:{configurationId}:{timestamp}"`
- Handler is idempotent: checks if configuration exists before processing
- Multiple triggers create separate executions (by design for testing/support scenarios)

---

## Non-Goals (Out of Scope)

*Explicitly state what this feature does NOT include to prevent scope creep.*

- ❌ Cancellation of in-progress file checks (not in this enhancement)
- ❌ Bulk triggering of multiple configurations at once (one-at-a-time only)
- ❌ Scheduling one-time custom file checks with different patterns (uses existing configuration only)
- ❌ Client-facing UI for self-service triggering (API only - authorization policies can be extended later)
- ❌ Rate limiting or throttling of manual triggers (relies on existing concurrency semaphore limit of 100)
- ❌ Historical view of who triggered which checks (audit log can consume FileCheckTriggered event for this)

---

## Success Criteria (Testable)

- [ ] **SC-001**: Support engineers can trigger file checks for their assigned clients via API and receive confirmation within 2 seconds
- [ ] **SC-002**: Security trimming prevents cross-client access - 100% of unauthorized trigger attempts return 403 Forbidden without executing checks
- [ ] **SC-003**: Every file check execution (scheduled or manual) publishes a FileCheckTriggered event before processing begins, enabling full audit trails
- [ ] **SC-004**: Manual triggers integrate seamlessly with existing concurrency controls - system maintains 100 concurrent check limit regardless of trigger source
- [ ] **SC-005**: Monitoring dashboards can differentiate manual vs scheduled executions for trend analysis (via IsManualTrigger field)

---

## Definition of Done

- [ ] All acceptance criteria have passing tests (xUnit unit + integration tests)
- [ ] FileCheckTriggered event includes correlation IDs in all log statements
- [ ] Idempotency verified (duplicate FileCheckTriggered event test passes)
- [ ] Domain test coverage ≥90%, handler coverage ≥80%
- [ ] API endpoint follows existing security pattern (JWT claims extraction)
- [ ] OpenAPI/Swagger documentation updated for new endpoint
- [ ] PR approved, merged to main

---

## Assumptions

1. **JWT Token Structure**: Support engineers have JWT tokens with `clientId` claim representing their assigned client (or multiple clients for multi-client support staff)
2. **Authorization Policy**: The existing `ClientAccess` policy is sufficient - no new policy creation needed
3. **ExecutionId Generation**: Handler generates ExecutionId (Guid.NewGuid()) - API doesn't need to provide it
4. **Concurrency Behavior**: Manual triggers participate in the same concurrency semaphore (100 limit) as scheduled checks - no separate queue needed
5. **Response Time**: API responds immediately with 202 Accepted after sending command - does not wait for file check completion (async pattern)
6. **Error Recovery**: Failed manual triggers follow existing NServiceBus retry policies - no special retry logic needed for manual vs scheduled
7. **Configuration State**: Only active configurations (IsActive=true) can be manually triggered - inactive/deleted configurations return 404
8. **Audit Trail**: FileCheckTriggered event provides sufficient audit data (triggered by, timestamp, manual flag) - no separate audit log needed

---

## Notes / Open Questions

**Dependencies**: 
- Requires existing ExecuteFileCheck command and handler (already implemented)
- Requires existing security trimming pattern with JWT claims (already implemented)
- Requires configuration existence validation (already available)

**Design Considerations**:
- API validates configuration before sending command to ensure fast failure for invalid requests
- Event published at start of command processing to capture all execution attempts
- User identifier needs to flow through command to populate event's "triggered by" field for manual triggers

**Future Enhancements** (not in scope):
- Client self-service portal for manual triggering
- Bulk trigger API for multiple configurations
- Scheduled one-time checks with custom parameters
