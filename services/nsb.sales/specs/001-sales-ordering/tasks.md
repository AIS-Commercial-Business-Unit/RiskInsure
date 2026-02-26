# Tasks: Sales Ordering (Place Order)

**Input**: Design documents from `services/nsb.sales/specs/001-sales-ordering/`
**Prerequisites**: plan.md (required), spec.md (required for user stories)
**Constitution**: [.specify/memory/constitution.md](../../../../.specify/memory/constitution.md)

**Tests**: Include tests for this feature to satisfy constitution coverage and idempotency verification requirements.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Create service folders in `services/nsb.sales/src/{Api,Domain,Infrastructure,Endpoint.In}` and `services/nsb.sales/test/{Unit.Tests,Integration.Tests}`
- [ ] T002 Create/verify project files in `services/nsb.sales/src/{Api,Domain,Infrastructure,Endpoint.In}/*.csproj`
- [ ] T003 [P] Add service projects to `RiskInsure.slnx`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

- [ ] T004 Define command contract in `services/nsb.sales/src/Domain/Contracts/Commands/PlaceOrder.cs` (include MessageId, OccurredUtc, IdempotencyKey)
- [ ] T005 Define event contract in `services/nsb.sales/src/Domain/Contracts/Events/OrderPlaced.cs` (include MessageId, OccurredUtc, IdempotencyKey)
- [ ] T006 [P] Create domain DTO/model in `services/nsb.sales/src/Domain/{DTOs,Models}/Order.cs` with initial state `Placed`
- [ ] T007 [P] Create domain manager interface/implementation in `services/nsb.sales/src/Domain/Managers/{IOrderManager.cs,OrderManager.cs}`
- [ ] T008 [P] Create domain repository interface in `services/nsb.sales/src/Domain/Services/IOrderRepository.cs`
- [ ] T009 Configure Cosmos initialization in `services/nsb.sales/src/Infrastructure/CosmosDbInitializer.cs` with partition key `/orderId`
- [ ] T010 Configure NServiceBus in `services/nsb.sales/src/Infrastructure/NServiceBusConfigurationExtensions.cs`
- [ ] T011 Configure appsettings templates in `services/nsb.sales/src/{Api,Endpoint.In}/appsettings.Development.json.template`
- [ ] T012 Configure structured logging + correlation fields in `services/nsb.sales/src/{Api,Endpoint.In}/Program.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Place Order (Priority: P1) 🎯 MVP

**Goal**: Accept an order request and publish `OrderPlaced` for downstream services.

**Independent Test**: Submit a place-order request and verify one `OrderPlaced` event per `OrderId`, including duplicate-request idempotency behavior.

### Tests for User Story 1

- [ ] T013 [P] [US1] Add manager unit tests in `services/nsb.sales/test/Unit.Tests/Managers/OrderManagerTests.cs` (invariants + duplicate handling)
- [ ] T014 [P] [US1] Add handler unit tests in `services/nsb.sales/test/Unit.Tests/Handlers/PlaceOrderHandlerTests.cs` (publish once + duplicate short-circuit)
- [ ] T015 [P] [US1] Add integration tests in `services/nsb.sales/test/Integration.Tests/tests/place-order.spec.ts` (API to event flow)

### Implementation for User Story 1

- [ ] T016 [US1] Implement manager orchestration in `services/nsb.sales/src/Domain/Managers/OrderManager.cs` (validate, idempotency check, create order)
- [ ] T017 [US1] Implement repository in `services/nsb.sales/src/Domain/Services/SalesDb/OrderRepository.cs` (read by OrderId, idempotent create)
- [ ] T018 [US1] Implement message handler in `services/nsb.sales/src/Endpoint.In/Handlers/PlaceOrderHandler.cs` (thin handler delegates to manager and publishes `OrderPlaced`)
- [ ] T019 [US1] Implement API controller in `services/nsb.sales/src/Api/Controllers/OrdersController.cs` to submit place-order command
- [ ] T020 [US1] Add API request/response models in `services/nsb.sales/src/Api/Models/{PlaceOrderRequest.cs,PlaceOrderResponse.cs}`
- [ ] T021 [US1] Wire DI registrations in `services/nsb.sales/src/{Api,Endpoint.In}/Program.cs`
- [ ] T022 [US1] Add public contract conformance check for `OrderPlaced` in `platform/RiskInsure.PublicContracts/Events/` (or explicitly document internal-only contract)

**Checkpoint**: User Story 1 is functional and independently testable

---

## Phase 4: Polish & Cross-Cutting Concerns

- [ ] T023 [P] Update feature index in `services/nsb.sales/docs/specs/README.md`
- [ ] T024 [P] Add service README in `services/nsb.sales/README.md` (run/debug/test instructions)
- [ ] T025 Resolve spec open question by confirming state naming (`Placed`) in `services/nsb.sales/specs/001-sales-ordering/spec.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS user story implementation
- **User Story 1 (Phase 3)**: Depends on Foundational completion
- **Polish (Phase 4)**: Depends on User Story 1 completion

### Within User Story 1

- T016 depends on T004-T008
- T017 depends on T008-T010
- T018 depends on T005, T016, T017
- T019/T020 depend on T004 and T016
- T021 depends on T016-T020
- T013-T015 can run in parallel after foundational setup

---

## Parallel Example: User Story 1

```bash
Task: "Add manager unit tests in services/nsb.sales/test/Unit.Tests/Managers/OrderManagerTests.cs"
Task: "Add handler unit tests in services/nsb.sales/test/Unit.Tests/Handlers/PlaceOrderHandlerTests.cs"
Task: "Add integration tests in services/nsb.sales/test/Integration.Tests/tests/place-order.spec.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. Validate User Story 1 independently

### Incremental Delivery

- Complete polish tasks after MVP validation
