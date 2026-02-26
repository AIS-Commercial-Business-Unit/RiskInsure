# Tasks: Manual File Check Trigger API

**Input**: Design documents from `services/file-retrieval/specs/001-file-retrieval-config/`  
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓, quickstart.md ✓  
**Constitution**: [.specify/memory/constitution.md](../../../../.specify/memory/constitution.md)

**Tests**: This feature includes comprehensive test coverage as specified in the Definition of Done (handler coverage ≥80%, domain coverage ≥90%).

**Organization**: Tasks are organized by implementation layer to enable efficient completion of this enhancement to existing infrastructure.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1)
- Include exact file paths in descriptions

---

## Phase 1: Message Contracts (Foundation)

**Purpose**: Define the message contracts that all other layers depend on

- [ ] T001 [P] Add TriggeredBy field to ExecuteFileCheck command in `services/file-retrieval/src/FileRetrieval.Contracts/Commands/ExecuteFileCheck.cs`
- [ ] T002 [P] Create FileCheckTriggered event contract in `services/file-retrieval/src/FileRetrieval.Contracts/Events/FileCheckTriggered.cs`

**Checkpoint**: Message contracts defined - can proceed with implementation layers

---

## Phase 2: User Story 1 - Support Engineer Manually Triggers File Check (Priority: P1) 🎯 MVP

**Goal**: Enable support engineers to manually trigger file checks for client configurations they have access to, with immediate confirmation and tracking ID.

**Independent Test**: 
1. Support engineer authenticates with JWT containing clientId claim
2. POST to `/api/configuration/{configurationId}/trigger` for owned configuration
3. Receives 202 Accepted with ExecutionId
4. FileCheckTriggered event is published with audit details
5. File check executes (visible in execution history API)

### Application Layer Changes

- [ ] T003 Update FileCheckService.ExecuteCheckAsync signature to accept executionId parameter in `services/file-retrieval/src/FileRetrieval.Application/Services/FileCheckService.cs`
- [ ] T004 Modify ExecuteFileCheckHandler to generate ExecutionId and publish FileCheckTriggered event before processing in `services/file-retrieval/src/FileRetrieval.Application/MessageHandlers/ExecuteFileCheckHandler.cs`

### API Layer Implementation

- [ ] T005 [P] Create TriggerFileCheckResponse DTO in `services/file-retrieval/src/FileRetrieval.API/Models/TriggerFileCheckResponse.cs`
- [ ] T006 Add TriggerFileCheck endpoint to ConfigurationController in `services/file-retrieval/src/FileRetrieval.API/Controllers/ConfigurationController.cs`

### Unit Tests for Application Layer

- [ ] T007 [P] Add unit tests for FileCheckTriggered event publishing in `services/file-retrieval/test/FileRetrieval.Application.Tests/MessageHandlers/ExecuteFileCheckHandlerTests.cs`
- [ ] T008 [P] Add unit tests for FileCheckService executionId parameter in `services/file-retrieval/test/FileRetrieval.Application.Tests/Services/FileCheckServiceTests.cs`

### Integration Tests for API Layer

- [ ] T009 Add happy path test (202 Accepted) in `services/file-retrieval/test/FileRetrieval.Integration.Tests/Controllers/ConfigurationControllerTriggerTests.cs`
- [ ] T010 [P] Add security test (404 for wrong client) in `services/file-retrieval/test/FileRetrieval.Integration.Tests/Controllers/ConfigurationControllerTriggerTests.cs`
- [ ] T011 [P] Add validation test (400 for inactive config) in `services/file-retrieval/test/FileRetrieval.Integration.Tests/Controllers/ConfigurationControllerTriggerTests.cs`
- [ ] T012 [P] Add authentication test (401 for missing claim) in `services/file-retrieval/test/FileRetrieval.Integration.Tests/Controllers/ConfigurationControllerTriggerTests.cs`
- [ ] T013 [P] Add not found test (404 for non-existent config) in `services/file-retrieval/test/FileRetrieval.Integration.Tests/Controllers/ConfigurationControllerTriggerTests.cs`

**Checkpoint**: User Story 1 fully implemented and tested - feature complete and ready for deployment

---

## Phase 3: Documentation & Polish

**Purpose**: Update documentation and finalize release

- [ ] T014 [P] Update OpenAPI/Swagger documentation for new trigger endpoint in `services/file-retrieval/src/FileRetrieval.API/` (Program.cs or separate swagger config)
- [ ] T015 [P] Update file-retrieval-standards.md with manual trigger pattern in `services/file-retrieval/docs/file-retrieval-standards.md`
- [ ] T016 Verify all Definition of Done criteria are met (spec.md lines 190-198)

---

## Dependencies & Execution Order

### Phase Dependencies

```text
Phase 1: Message Contracts (T001-T002)
   ↓
Phase 2: User Story 1 Implementation (T003-T013)
   ├─ Application Layer (T003-T004) → API Layer (T005-T006)
   ├─ Unit Tests (T007-T008) - can run parallel with implementation
   └─ Integration Tests (T009-T013) - requires T005-T006 complete
   ↓
Phase 3: Documentation & Polish (T014-T016)
```

### Critical Path

**Sequential (must complete in order)**:
1. T001 or T002 (contracts) → T004 (handler modification) → T009-T013 (integration tests)
2. T003 (service signature) → T004 (handler uses new signature)
3. T004 (handler complete) → T006 (API endpoint sends command)

**Parallel Opportunities**:
- T001 and T002 (different contract files)
- T005 and T003 (different layers, no dependencies)
- T007 and T008 (different test files)
- T009, T010, T011, T012, T013 (different test scenarios)
- T014 and T015 (different documentation files)

### Dependency Graph

```text
                    ┌─ T001 (ExecuteFileCheck) ─┐
                    │                            ├─→ T004 (Handler) ─→ T006 (API) ─→ T009-T013 (Integration Tests)
                    └─ T002 (FileCheckTriggered) ┘         ↑
                                                           │
                    T003 (Service Signature) ──────────────┘
                                                           │
                    T005 (Response DTO) ─────────────→ T006 (API)
                    
                    T007, T008 (Unit Tests) ─────→ Parallel with T004-T006
                    
                    T014, T015 (Docs) ───────────→ After T009-T013 complete
```

---

## Parallel Execution Example

### After Phase 1 Complete (Contracts Done)

**Launch in parallel**:
```bash
# Session 1: Service signature change
Task T003: Update FileCheckService signature

# Session 2: API response model
Task T005: Create TriggerFileCheckResponse DTO

# Session 3: Unit test preparation
Task T007: Add handler event publishing tests
Task T008: Add service executionId tests
```

### After Application Layer Complete (T003-T004)

**Launch in parallel**:
```bash
# Session 1: API endpoint
Task T006: Add TriggerFileCheck endpoint

# Session 2-6: Integration tests (all parallel)
Task T009: Happy path test (202)
Task T010: Security test (404 wrong client)
Task T011: Validation test (400 inactive)
Task T012: Auth test (401 missing claim)
Task T013: Not found test (404)
```

### Final Phase (Documentation)

**Launch in parallel**:
```bash
# Session 1: API documentation
Task T014: Update Swagger docs

# Session 2: Standards documentation
Task T015: Update file-retrieval-standards.md
```

---

## Implementation Strategy

### MVP Scope (Minimum Viable Product)

**What to build first**: User Story 1 only (Phase 1 + Phase 2)
- Message contracts (T001-T002)
- Application layer changes (T003-T004)
- API endpoint (T005-T006)
- Core tests (T007-T013)

**Why this is the MVP**:
- Delivers complete manual trigger capability
- Includes audit trail via FileCheckTriggered event
- Security-trimmed and tested
- Can be deployed and used immediately
- Estimated effort: 6-8 hours

**Post-MVP additions**:
- Documentation polish (T014-T016)
- Optional: Event subscriber implementations (monitoring dashboard, audit log)
- Optional: Client self-service UI (requires authorization policy extension)

### Incremental Delivery

Since this feature is a single user story enhancement:

1. **Sprint 1, Week 1**: Complete Phase 1 + Phase 2 (T001-T013) → Deploy to staging
2. **Sprint 1, Week 2**: Complete Phase 3 (T014-T016) → Deploy to production
3. Each phase adds measurable value without breaking existing functionality

### Rollback Plan

If issues arise after deployment:
1. **API rollback**: Revert FileRetrieval.API deployment → Removes trigger endpoint
2. **Worker rollback**: Revert FileRetrieval.Worker deployment → Removes event publishing
3. **No data corruption**: All changes are code-only, backward compatible

---

## Task Details

### T001: Add TriggeredBy field to ExecuteFileCheck command
**File**: `services/file-retrieval/src/FileRetrieval.Contracts/Commands/ExecuteFileCheck.cs`  
**Changes**: Add `public string? TriggeredBy { get; init; }` field  
**Validation**: Field is nullable (backward compatible)  
**Estimated time**: 15 minutes

### T002: Create FileCheckTriggered event contract
**File**: `services/file-retrieval/src/FileRetrieval.Contracts/Events/FileCheckTriggered.cs`  
**Content**: Event record with 12 fields (4 metadata, 4 context, 2 tracking, 2 trigger)  
**Pattern**: Follows existing event patterns (FileCheckCompleted, FileCheckFailed)  
**Estimated time**: 30 minutes

### T003: Update FileCheckService.ExecuteCheckAsync signature
**File**: `services/file-retrieval/src/FileRetrieval.Application/Services/FileCheckService.cs`  
**Change**: Add `Guid executionId` parameter, use it instead of generating internally  
**Impact**: Breaking change for 2 callers (handler + tests)  
**Estimated time**: 30 minutes

### T004: Modify ExecuteFileCheckHandler
**File**: `services/file-retrieval/src/FileRetrieval.Application/MessageHandlers/ExecuteFileCheckHandler.cs`  
**Changes**:
1. Generate `executionId = Guid.NewGuid()` after loading configuration
2. Publish FileCheckTriggered event before calling FileCheckService
3. Pass executionId to FileCheckService.ExecuteCheckAsync()
**Pattern**: Event publishing before service call (audit trail)  
**Estimated time**: 45 minutes

### T005: Create TriggerFileCheckResponse DTO
**File**: `services/file-retrieval/src/FileRetrieval.API/Models/TriggerFileCheckResponse.cs`  
**Content**: Record with 4 fields (ConfigurationId, ExecutionId, TriggeredAt, Message)  
**Pattern**: Response DTO for 202 Accepted async operations  
**Estimated time**: 15 minutes

### T006: Add TriggerFileCheck endpoint
**File**: `services/file-retrieval/src/FileRetrieval.API/Controllers/ConfigurationController.cs`  
**Route**: `POST /api/configuration/{configurationId}/trigger`  
**Logic**:
1. Extract clientId and userId from JWT claims
2. Validate configuration exists and is active
3. Generate executionId
4. Send ExecuteFileCheck command with IsManualTrigger=true
5. Return 202 Accepted with TriggerFileCheckResponse
**Security**: Client-scoped validation via existing ConfigurationService  
**Estimated time**: 1 hour

### T007: Add handler event publishing tests
**File**: `services/file-retrieval/test/FileRetrieval.Application.Tests/MessageHandlers/ExecuteFileCheckHandlerTests.cs`  
**Scenarios**:
- Verify FileCheckTriggered published before service call
- Verify event includes correct trigger context (manual vs scheduled)
- Verify IdempotencyKey format matches specification
- Verify TriggeredBy defaults to "Scheduler" when null
**Coverage target**: ≥80% for handler  
**Estimated time**: 1 hour

### T008: Add service executionId parameter tests
**File**: `services/file-retrieval/test/FileRetrieval.Application.Tests/Services/FileCheckServiceTests.cs`  
**Scenarios**:
- Verify service uses provided executionId instead of generating new one
- Update existing tests to pass executionId parameter
**Estimated time**: 30 minutes

### T009: Add happy path integration test
**File**: `services/file-retrieval/test/FileRetrieval.Integration.Tests/Controllers/ConfigurationControllerTriggerTests.cs`  
**Scenario**: Valid configuration, returns 202 with ExecutionId  
**Validation**: Response structure, status code, executionId present  
**Estimated time**: 30 minutes

### T010-T013: Add error scenario integration tests
**File**: `services/file-retrieval/test/FileRetrieval.Integration.Tests/Controllers/ConfigurationControllerTriggerTests.cs`  
**Scenarios**:
- T010: Wrong client returns 404 (security trimming)
- T011: Inactive configuration returns 400
- T012: Missing clientId claim returns 401
- T013: Non-existent configuration returns 404
**Estimated time**: 1.5 hours total

### T014: Update OpenAPI/Swagger documentation
**File**: `services/file-retrieval/src/FileRetrieval.API/Program.cs` or Swagger config  
**Changes**: Document new POST endpoint, request/response models, error codes  
**Estimated time**: 30 minutes

### T015: Update file-retrieval-standards.md
**File**: `services/file-retrieval/docs/file-retrieval-standards.md`  
**Changes**: Add manual trigger pattern, event publishing pattern, audit trail guidelines  
**Estimated time**: 30 minutes

### T016: Verify Definition of Done
**Checklist**: Verify all DoD criteria from spec.md (lines 190-198)  
**Items**: Tests passing, correlation IDs present, idempotency verified, coverage targets met, security pattern followed, documentation updated  
**Estimated time**: 30 minutes

---

## Implementation Strategy

### Single Story Feature

This feature consists of a single user story (Support Engineer Manually Triggers File Check) organized into logical implementation phases:

1. **Phase 1**: Contracts (foundation for all changes)
2. **Phase 2**: Implementation layers (service → handler → API)
3. **Phase 3**: Documentation and verification

### Recommended Execution Order

**Day 1: Contracts + Core Logic (4-5 hours)**
1. T001, T002 (contracts) - 45 min
2. T003 (service signature) - 30 min
3. T004 (handler modification) - 45 min
4. T007, T008 (unit tests) - 1.5 hours
5. Verify: Unit tests pass

**Day 2: API + Integration Tests (3-4 hours)**
1. T005 (response DTO) - 15 min
2. T006 (API endpoint) - 1 hour
3. T009-T013 (integration tests) - 2 hours
4. Verify: All tests pass, coverage targets met

**Day 3: Documentation + Review (1-2 hours)**
1. T014 (Swagger docs) - 30 min
2. T015 (standards docs) - 30 min
3. T016 (DoD verification) - 30 min
4. Code review + fixes

**Total Estimated Effort**: 8-11 hours (including testing and documentation)

---

## Success Metrics Validation

### SC-001: API response time <2 seconds
**Validation**: Integration test T009 measures response time  
**Expected**: ~20-70ms (well under target)  
**Test**: Add assertion to verify response time <2s

### SC-002: 100% security trimming compliance
**Validation**: Integration test T010 verifies 404 for wrong client  
**Test**: Automated test proves no cross-client access possible

### SC-003: Every file check publishes FileCheckTriggered
**Validation**: Unit test T007 verifies event publishing  
**Test**: Mock verification proves event published before service call

### SC-004: Concurrency controls maintained (100 limit)
**Validation**: No new test needed - existing semaphore applies to all triggers  
**Verification**: Code review confirms no separate execution path

### SC-005: Monitoring can differentiate trigger types
**Validation**: Unit test T007 verifies IsManualTrigger and TriggeredBy fields populated  
**Test**: Assert event contains correct values for manual vs scheduled

---

## Parallel Opportunities

### After T001-T002 Complete (Contracts)

**Can proceed in parallel**:
- T003 (service signature) 
- T005 (response DTO)
- T007 (handler tests - can write tests against contract)

### After T003-T004 Complete (Application Layer)

**Can proceed in parallel**:
- T006 (API endpoint)
- T008 (service tests - update for new signature)
- T009-T013 (all integration tests - different scenarios)

### After T006 Complete (API Endpoint)

**Can proceed in parallel**:
- T009, T010, T011, T012, T013 (all integration test scenarios)
- T014 (Swagger docs)
- T015 (standards docs)

---

## Risk Mitigation

### Risk 1: Service signature change breaks existing callers
**Mitigation**: Only 2 callers (ExecuteFileCheckHandler + unit tests), both updated in same PR  
**Validation**: Build must pass before PR approval

### Risk 2: Event publishing increases handler latency
**Mitigation**: Event publishing adds ~10-20ms, negligible vs file check duration  
**Validation**: Performance test in T007 can measure handler execution time

### Risk 3: Concurrent manual + scheduled triggers
**Mitigation**: Existing concurrency semaphore (100 limit) handles both trigger types  
**Validation**: No new code needed - existing pattern proven in production

### Risk 4: Missing clientId claim in JWT
**Mitigation**: Existing GetClientIdFromClaims() throws clear exception with 401 response  
**Validation**: Test T012 proves correct error handling

---

## Definition of Done Checklist

Per spec.md (lines 190-198):

- [ ] All acceptance criteria have passing tests (T007-T013 cover all 5 acceptance criteria)
- [ ] FileCheckTriggered event includes correlation IDs in all log statements (T004 implementation)
- [ ] Idempotency verified (T007 includes duplicate event test)
- [ ] Domain test coverage ≥90%, handler coverage ≥80% (T007-T008 achieve targets)
- [ ] API endpoint follows existing security pattern (T006 uses JWT claims extraction)
- [ ] OpenAPI/Swagger documentation updated for new endpoint (T014)
- [ ] PR approved, merged to main

---

## Quick Reference

**Total Tasks**: 16 tasks  
**Estimated Effort**: 8-11 hours (including comprehensive testing)  
**MVP Scope**: All tasks (single user story feature)  
**Parallel Opportunities**: 8 tasks can run in parallel (marked with [P])  
**Critical Path**: T001/T002 → T003 → T004 → T006 → T009-T013 (integration tests)

**Key Files Modified/Created**:
- ✨ 1 command modified (ExecuteFileCheck - add TriggeredBy field)
- ✨ 1 event created (FileCheckTriggered)
- ✨ 1 handler modified (ExecuteFileCheckHandler - publish event)
- ✨ 1 service modified (FileCheckService - accept executionId parameter)
- ✨ 1 controller modified (ConfigurationController - add trigger endpoint)
- ✨ 1 response DTO created (TriggerFileCheckResponse)
- ✨ 2 test files created/extended (handler tests + integration tests)
- ✨ 2 documentation files updated (Swagger + standards)

**Next Steps**:
1. Create feature branch: `git checkout -b 001-file-retrieval-config`
2. Start with T001-T002 (contracts)
3. Proceed through tasks in dependency order
4. Run tests after each phase
5. Complete T016 (DoD verification) before PR submission

---

**Document Status**: ✅ Ready for implementation - All tasks defined with concrete paths and clear dependencies
