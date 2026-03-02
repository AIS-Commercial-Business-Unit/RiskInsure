---

description: "Task list template for feature implementation"
---

# Tasks: [FEATURE NAME]

**Input**: Design documents from `/services/<domain>/specs/[###-feature-name]/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/
**Constitution**: [.specify/memory/constitution.md](../../.specify/memory/constitution.md)

**Tests**: The examples below include test tasks. Tests are OPTIONAL - only include them if explicitly requested in the feature specification.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions (RiskInsure)

- **Service root**: `services/<domain>/` (e.g., `services/billing/`)
- **Domain layer**: `services/<domain>/src/Domain/` (contracts, models, managers)
- **Infrastructure**: `services/<domain>/src/Infrastructure/` (handlers, repositories)
- **API**: `services/<domain>/src/Api/` (HTTP endpoints)
- **Endpoint**: `services/<domain>/src/Endpoint.In/` (NServiceBus host)
- **Tests**: `services/<domain>/test/{Unit.Tests,Integration.Tests}/`
- **Public contracts**: `platform/RiskInsure.PublicContracts/{Events,Commands,POCOs}/`

## Implementation Order (RiskInsure Layering)

**DEPENDENCY RULE**: Inward dependencies only (Api → Domain, Infrastructure → Domain, Endpoint → Infrastructure → Domain)

1. **Domain layer first** - Contracts, DTOs, managers, models, services
2. **Infrastructure layer** - Shared configuration (Cosmos init, NServiceBus extensions)
3. **Endpoint.In layer** - Message handlers delegating to domain managers
4. **API layer** - Controllers/endpoints and request models
5. **Tests** - Unit tests (`Unit.Tests`) and integration tests (`Integration.Tests`)

<!-- 
  ============================================================================
  IMPORTANT: The tasks below are SAMPLE TASKS for illustration purposes only.
  
  The /speckit.tasks command MUST replace these with actual tasks based on:
  - User stories from spec.md (with their priorities P1, P2, P3...)
  - Feature requirements from plan.md
  - Entities from data-model.md
  - Endpoints from contracts/
  
  Tasks MUST be organized by user story so each story can be:
  - Implemented independently
  - Tested independently
  - Delivered as an MVP increment
  
  DO NOT keep these sample tasks in the generated tasks.md file.
  ============================================================================
-->

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Create service folders in `services/<domain>/src/{Api,Domain,Infrastructure,Endpoint.In}` and `services/<domain>/test/{Unit.Tests,Integration.Tests}`
- [ ] T002 Create/verify project files in `services/<domain>/src/{Api,Domain,Infrastructure,Endpoint.In}/*.csproj`
- [ ] T003 [P] Add service projects to `RiskInsure.slnx`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

RiskInsure foundational tasks (adjust based on your feature):

- [ ] T004 Define command contracts in `services/<domain>/src/Domain/Contracts/Commands/` (C# records with MessageId, OccurredUtc, IdempotencyKey)
- [ ] T005 Define event contracts in `services/<domain>/src/Domain/Contracts/Events/` (past-tense names + required metadata)
- [ ] T006 [P] Create domain DTOs and models in `services/<domain>/src/Domain/{DTOs,Models}/`
- [ ] T007 [P] Create domain manager interfaces and implementations in `services/<domain>/src/Domain/Managers/`
- [ ] T008 [P] Create domain services/repository interfaces in `services/<domain>/src/Domain/Services/`
- [ ] T009 Configure Cosmos initialization in `services/<domain>/src/Infrastructure/CosmosDbInitializer.cs` (container + partition key)
- [ ] T010 Configure NServiceBus in `services/<domain>/src/Infrastructure/NServiceBusConfigurationExtensions.cs`
- [ ] T011 Configure appsettings templates in `services/<domain>/src/{Api,Endpoint.In}/appsettings.Development.json.template`
- [ ] T012 Configure structured logging and correlation fields in `services/<domain>/src/{Api,Endpoint.In}/Program.cs`
- [ ] T012a Configure host-appropriate Serilog packages in `services/<domain>/src/{Api,Endpoint.In}/*.csproj` (`Serilog.AspNetCore` for Api; `Serilog.Extensions.Hosting` + `Serilog.Settings.Configuration` for Endpoint.In)
- [ ] T012b Configure Docker runtime base images in `services/<domain>/src/{Api,Endpoint.In}/Dockerfile` (`aspnet:10.0` for Api; `runtime:10.0` for Endpoint.In)
- [ ] T012c Validate package/runtime compatibility by building executable projects and images (fail fast on framework mismatch)

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - [Title] (Priority: P1) 🎯 MVP

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 1 (OPTIONAL - only if tests requested) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T013 [P] [US1] Add unit tests for manager behavior in `services/<domain>/test/Unit.Tests/Managers/[ManagerName]Tests.cs`
- [ ] T014 [P] [US1] Add integration tests for endpoint flow in `services/<domain>/test/Integration.Tests/tests/[feature].spec.ts`

### Implementation for User Story 1

- [ ] T015 [P] [US1] Implement domain manager orchestration in `services/<domain>/src/Domain/Managers/[ManagerName].cs`
- [ ] T016 [US1] Implement message handler in `services/<domain>/src/Endpoint.In/Handlers/[MessageName]Handler.cs` (thin handler, delegates to manager)
- [ ] T017 [US1] Implement API endpoint/controller in `services/<domain>/src/Api/Controllers/[Feature]Controller.cs`
- [ ] T018 [US1] Add request/response models in `services/<domain>/src/Api/Models/`
- [ ] T019 [US1] Wire DI registrations in `services/<domain>/src/{Api,Endpoint.In}/Program.cs`

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - [Title] (Priority: P2)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 2 (OPTIONAL - only if tests requested) ⚠️

- [ ] T020 [P] [US2] Add unit tests in `services/<domain>/test/Unit.Tests/`
- [ ] T021 [P] [US2] Add integration tests in `services/<domain>/test/Integration.Tests/tests/`

### Implementation for User Story 2

- [ ] T022 [P] [US2] Extend domain models/contracts in `services/<domain>/src/Domain/`
- [ ] T023 [US2] Implement handler changes in `services/<domain>/src/Endpoint.In/Handlers/`
- [ ] T024 [US2] Implement API changes in `services/<domain>/src/Api/Controllers/`
- [ ] T025 [US2] Integrate with User Story 1 components (if needed)

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - [Title] (Priority: P3)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 3 (OPTIONAL - only if tests requested) ⚠️

- [ ] T026 [P] [US3] Add unit tests in `services/<domain>/test/Unit.Tests/`
- [ ] T027 [P] [US3] Add integration tests in `services/<domain>/test/Integration.Tests/tests/`

### Implementation for User Story 3

- [ ] T028 [P] [US3] Extend domain contracts/models in `services/<domain>/src/Domain/`
- [ ] T029 [US3] Implement handler updates in `services/<domain>/src/Endpoint.In/Handlers/`
- [ ] T030 [US3] Implement API updates in `services/<domain>/src/Api/Controllers/`

**Checkpoint**: All user stories should now be independently functional

---

[Add more user story phases as needed, following the same pattern]

---

## Phase N: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] TXXX [P] Documentation updates in docs/
- [ ] TXXX Code cleanup and refactoring
- [ ] TXXX Performance optimization across all stories
- [ ] TXXX [P] Additional unit tests (if requested) in `services/<domain>/test/Unit.Tests/`
- [ ] TXXX Security hardening
- [ ] TXXX Run quickstart.md validation

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - May integrate with US1 but should be independently testable
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - May integrate with US1/US2 but should be independently testable

### Within Each User Story

- Tests (if included) MUST be written and FAIL before implementation
- Models before services
- Services before endpoints
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- All Foundational tasks marked [P] can run in parallel (within Phase 2)
- Once Foundational phase completes, all user stories can start in parallel (if team capacity allows)
- All tests for a user story marked [P] can run in parallel
- Models within a story marked [P] can run in parallel
- Different user stories can be worked on in parallel by different team members

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together (if tests requested):
Task: "Unit tests for [manager] in services/<domain>/test/Unit.Tests/Managers/[ManagerName]Tests.cs"
Task: "Integration tests for [journey] in services/<domain>/test/Integration.Tests/tests/[feature].spec.ts"

# Launch all models for User Story 1 together:
Task: "Create command contract in services/<domain>/src/Domain/Contracts/Commands/[CommandName].cs"
Task: "Create event contract in services/<domain>/src/Domain/Contracts/Events/[EventName].cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1
   - Developer B: User Story 2
   - Developer C: User Story 3
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
