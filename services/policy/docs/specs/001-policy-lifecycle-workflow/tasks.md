# Tasks: Policy Lifecycle Management Workflow

**Input**: Design documents from `/services/policy/docs/specs/001-policy-lifecycle-workflow/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/
**Constitution**: `.specify/memory/constitution.md`

**Tests**: Included because spec and DoD explicitly require xUnit + Playwright coverage.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare Policy service structure for lifecycle workflow work.

- [X] T001 Verify Policy projects exist and build targets are present in `services/policy/src/{Api,Domain,Infrastructure,Endpoint.In}/*.csproj`
- [X] T002 Create lifecycle folders in `services/policy/src/Domain/Contracts/Commands`, `services/policy/src/Domain/Contracts/Events`, and `services/policy/src/Endpoint.In/Sagas`
- [X] T003 [P] Create lifecycle test folders in `services/policy/test/Unit.Tests/{Sagas,Lifecycle}` and `services/policy/test/Integration.Tests/tests/lifecycle`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core contracts, saga state, and wiring required before user stories.

- [X] T004 Create command contract `StartPolicyLifecycle` in `services/policy/src/Domain/Contracts/Commands/StartPolicyLifecycle.cs`
- [X] T005 [P] Create command contract `ApplyPolicyLifecycleMilestone` in `services/policy/src/Domain/Contracts/Commands/ApplyPolicyLifecycleMilestone.cs`
- [X] T006 [P] Create command contract `ProcessPolicyEquityUpdate` in `services/policy/src/Domain/Contracts/Commands/ProcessPolicyEquityUpdate.cs`
- [X] T007 [P] Create event contract `PolicyRenewalWindowOpened` in `services/policy/src/Domain/Contracts/Events/PolicyRenewalWindowOpened.cs`
- [X] T008 [P] Create event contract `PolicyTermCompleted` in `services/policy/src/Domain/Contracts/Events/PolicyTermCompleted.cs`
- [X] T009 [P] Create event contract `PolicyEquityLevelReported` in `services/policy/src/Domain/Contracts/Events/PolicyEquityLevelReported.cs`
- [X] T010 [P] Create event contract `PolicyRenewalAccepted` in `services/policy/src/Domain/Contracts/Events/PolicyRenewalAccepted.cs`
- [X] T011 Create lifecycle model `PolicyLifecycleTermState` in `services/policy/src/Domain/Models/PolicyLifecycleTermState.cs`
- [X] T012 [P] Create lifecycle milestone model in `services/policy/src/Domain/Models/PolicyLifecycleMilestone.cs`
- [X] T013 [P] Create lifecycle manager interface in `services/policy/src/Domain/Managers/IPolicyLifecycleManager.cs`
- [X] T014 Create lifecycle manager implementation skeleton in `services/policy/src/Domain/Managers/PolicyLifecycleManager.cs`
- [X] T015 [P] Create repository interface for term state in `services/policy/src/Domain/Repositories/IPolicyLifecycleTermStateRepository.cs`
- [X] T016 Create Cosmos repository implementation for term state in `services/policy/src/Domain/Repositories/PolicyLifecycleTermStateRepository.cs`
- [X] T017 Create saga data model in `services/policy/src/Endpoint.In/Sagas/PolicyLifecycleSagaData.cs`
- [X] T018 Create saga orchestrator with correlation mapping in `services/policy/src/Endpoint.In/Sagas/PolicyLifecycleSaga.cs`
- [X] T019 Register lifecycle dependencies in `services/policy/src/Endpoint.In/Program.cs`
- [X] T020 [P] Register lifecycle API dependencies in `services/policy/src/Api/Program.cs`

**Checkpoint**: Foundation complete; user stories can now be implemented and tested independently.

---

## Phase 3: User Story 1 - Term Lifecycle Orchestration (Priority: P1) 🎯 MVP

**Goal**: Start a policy-term workflow and progress through percentage-based milestones to terminal `Renewed` or `Expired` outcomes.

**Independent Test**: Starting a term and applying milestone messages transitions status deterministically and emits `PolicyRenewalWindowOpened` / `PolicyTermCompleted` as applicable.

### Tests for User Story 1

- [X] T021 [P] [US1] Add manager unit tests for term start/milestone transitions in `services/policy/test/Unit.Tests/Managers/PolicyLifecycleManagerTests.cs`
- [X] T022 [P] [US1] Add saga progression tests (start → renewal-open → term-end) in `services/policy/test/Unit.Tests/Sagas/PolicyLifecycleSagaTests.cs`
- [X] T023 [P] [US1] Add duplicate-message idempotency tests for lifecycle milestones in `services/policy/test/Unit.Tests/Sagas/PolicyLifecycleSagaIdempotencyTests.cs`
- [X] T024 [P] [US1] Add integration tests for lifecycle start/status API in `services/policy/test/Integration.Tests/tests/lifecycle/policy-lifecycle-core.spec.ts`

### Implementation for User Story 1

- [X] T025 [US1] Implement term start and milestone business logic in `services/policy/src/Domain/Managers/PolicyLifecycleManager.cs`
- [X] T026 [US1] Implement lifecycle command handler `StartPolicyLifecycleHandler` in `services/policy/src/Endpoint.In/Handlers/StartPolicyLifecycleHandler.cs`
- [X] T027 [US1] Implement lifecycle command handler `ApplyPolicyLifecycleMilestoneHandler` in `services/policy/src/Endpoint.In/Handlers/ApplyPolicyLifecycleMilestoneHandler.cs`
- [X] T028 [US1] Implement lifecycle query/start endpoints in `services/policy/src/Api/Controllers/PoliciesController.cs`
- [X] T029 [US1] Add lifecycle API request/response models in `services/policy/src/Api/Models/PolicyLifecycleModels.cs`

**Checkpoint**: US1 delivers independently testable policy-term lifecycle progression and completion semantics.

---

## Phase 4: User Story 2 - Billing Equity Cancellation Path (Priority: P2)

**Goal**: Apply billing equity signals to drive staged `PendingCancellation` and grace-window recheck before final cancellation.

**Independent Test**: Equity threshold breach enters `PendingCancellation`; grace recheck cancels only if equity remains below threshold, otherwise returns to `Active`.

### Tests for User Story 2

- [X] T030 [P] [US2] Add manager unit tests for equity breach and recovery behavior in `services/policy/test/Unit.Tests/Managers/PolicyLifecycleEquityTests.cs`
- [X] T031 [P] [US2] Add saga tests for pending-cancellation and grace recheck transitions in `services/policy/test/Unit.Tests/Sagas/PolicyLifecycleSagaCancellationTests.cs`
- [X] T032 [P] [US2] Add integration tests for equity-update API flow in `services/policy/test/Integration.Tests/tests/lifecycle/policy-lifecycle-equity.spec.ts`

### Implementation for User Story 2

- [X] T033 [US2] Implement equity update and grace-window logic in `services/policy/src/Domain/Managers/PolicyLifecycleManager.cs`
- [X] T034 [US2] Implement `ProcessPolicyEquityUpdate` handling in `services/policy/src/Endpoint.In/Handlers/ProcessPolicyEquityUpdateHandler.cs`
- [X] T035 [US2] Implement equity-update API endpoint in `services/policy/src/Api/Controllers/PoliciesController.cs`
- [X] T036 [US2] Emit `PolicyEquityLevelReported` events from lifecycle orchestration in `services/policy/src/Endpoint.In/Sagas/PolicyLifecycleSaga.cs`

**Checkpoint**: US2 adds independently testable billing-driven cancellation recommendation behavior.

---

## Phase 5: User Story 3 - Renewal Handoff and Overlapping Terms (Priority: P3)

**Goal**: Support renewal acceptance that completes term N and emits start signal for term N+1 while allowing overlap.

**Independent Test**: Renewal acceptance for current term emits completion + next-term start signal and both terms can be queried independently.

### Tests for User Story 3

- [ ] T037 [P] [US3] Add manager unit tests for renewal handoff and overlap in `services/policy/test/Unit.Tests/Managers/PolicyLifecycleRenewalTests.cs`
- [ ] T038 [P] [US3] Add saga tests for terminal `Renewed` completion and next-term start emission in `services/policy/test/Unit.Tests/Sagas/PolicyLifecycleSagaRenewalTests.cs`
- [ ] T039 [P] [US3] Add integration tests for overlapping term lifecycle queries in `services/policy/test/Integration.Tests/tests/lifecycle/policy-lifecycle-overlap.spec.ts`

### Implementation for User Story 3

- [ ] T040 [US3] Implement renewal acceptance handling in `services/policy/src/Domain/Managers/PolicyLifecycleManager.cs`
- [ ] T041 [US3] Handle `PolicyRenewalAccepted` in `services/policy/src/Endpoint.In/Sagas/PolicyLifecycleSaga.cs`
- [ ] T042 [US3] Publish term-completion and next-term-start events in `services/policy/src/Endpoint.In/Sagas/PolicyLifecycleSaga.cs`
- [ ] T043 [US3] Expose overlap term query support in `services/policy/src/Api/Controllers/PoliciesController.cs`

**Checkpoint**: US3 provides independently testable renewal handoff and overlapping term workflow support.

---

## Final Phase: Polish & Cross-Cutting Concerns

**Purpose**: Hardening and documentation across stories.

- [ ] T044 [P] Update Policy lifecycle technical docs in `services/policy/docs/technical/policy-technical-spec.md`
- [ ] T045 Add structured correlation logging fields in lifecycle handlers/saga in `services/policy/src/Endpoint.In/{Handlers,Sagas}/`
- [ ] T046 [P] Validate quickstart flow against implemented endpoints in `services/policy/docs/specs/001-policy-lifecycle-workflow/quickstart.md`
- [ ] T047 Run full Policy test suites and capture outcomes in `services/policy/docs/specs/001-policy-lifecycle-workflow/`

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup (Phase 1) has no dependencies.
- Foundational (Phase 2) depends on Setup and blocks all user stories.
- User Story phases (3-5) depend on Foundational; execute in priority order for MVP or in parallel by staffing.
- Final Phase depends on completion of desired stories.

### User Story Dependencies

- **US1 (P1)**: No story dependency after Foundational completion.
- **US2 (P2)**: Depends on US1 contracts/saga scaffolding from Foundational; functionally independent validation scenario.
- **US3 (P3)**: Depends on US1 lifecycle baseline; independent handoff/overlap validation scenario.

---

## Parallel Execution Examples

### User Story 1

- Run `T021` in `services/policy/test/Unit.Tests/Managers/PolicyLifecycleManagerTests.cs`
- Run `T022` in `services/policy/test/Unit.Tests/Sagas/PolicyLifecycleSagaTests.cs`
- Run `T024` in `services/policy/test/Integration.Tests/tests/lifecycle/policy-lifecycle-core.spec.ts`

### User Story 2

- Run `T030` in `services/policy/test/Unit.Tests/Managers/PolicyLifecycleEquityTests.cs`
- Run `T031` in `services/policy/test/Unit.Tests/Sagas/PolicyLifecycleSagaCancellationTests.cs`
- Run `T032` in `services/policy/test/Integration.Tests/tests/lifecycle/policy-lifecycle-equity.spec.ts`

### User Story 3

- Run `T037` in `services/policy/test/Unit.Tests/Managers/PolicyLifecycleRenewalTests.cs`
- Run `T038` in `services/policy/test/Unit.Tests/Sagas/PolicyLifecycleSagaRenewalTests.cs`
- Run `T039` in `services/policy/test/Integration.Tests/tests/lifecycle/policy-lifecycle-overlap.spec.ts`

---

## Implementation Strategy

### MVP First (US1)

1. Complete Phase 1 and Phase 2.
2. Complete US1 (Phase 3) and validate independent tests.
3. Demo/deploy lifecycle core behavior.

### Incremental Delivery

1. Add US2 staged cancellation and validate independently.
2. Add US3 renewal handoff/overlap and validate independently.
3. Execute Final Phase hardening and full regression.
