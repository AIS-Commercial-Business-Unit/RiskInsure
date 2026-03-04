# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/services/policy/docs/specs/001-policy-lifecycle-workflow/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Implement a policy-term lifecycle state machine in the Policy bounded context using an NServiceBus saga (`PolicyLifecycleSaga`) that orchestrates time-based lifecycle progression and billing-equity-driven cancellation. Lifecycle milestones are configured as percentages of total term ticks, translated to absolute timestamps at term start, and executed through message-driven transitions. Renewal acceptance completes the current term workflow and emits follow-up events to start a new term workflow instance.

## Technical Context (RiskInsure Stack)

**Language/Version**: .NET 10.0, C# 13 (nullable reference types enabled, LangVersion: latest)  
**Framework**: NServiceBus 9.x with Azure Service Bus transport  
**Messaging**: Azure Service Bus (commands via Send, events via Publish)  
**Testing**: xUnit 2.9.0 (unit/domain), Playwright (API integration via Node.js)  
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
**Rationale**: Existing Policy service persistence and repositories are Cosmos-based with `/policyId` partitioning. Lifecycle state transitions and policy-term state projections are naturally document-centric and partition-local.

### Project Structure Constraints

**Bounded Context**: Policy  
**Service Location**: `services/policy/`  
**Layer Requirements** (per `copilot-instructions/project-structure.md` and existing service patterns like `services/billing/`):
- `src/Domain/` - Contracts, DTOs, managers, models, services (business logic + orchestration)
- `src/Infrastructure/` - Shared infrastructure configuration (Cosmos init, NServiceBus config extensions)
- `src/Api/` - HTTP endpoints/controllers/models + Program/appsettings
- `src/Endpoint.In/` - NServiceBus message processing host + Handlers
- `test/Unit.Tests/` - xUnit unit tests
- `test/Integration.Tests/` - Playwright/API integration tests (Node.js/npm)

**Performance Goals**: Message-driven lifecycle transition handling <500ms p95 per message; lifecycle query API <200ms p95 under normal load.  
**Constraints**: Idempotent handlers/saga progression; thin handlers and saga orchestrator-only behavior; no direct external I/O from saga; atomic policy state updates within partition; percentage-based milestone computation.  
**Scale/Scope**: Supports variable tick counts per term (e.g., 6/10/24), overlapping consecutive term workflows per policy, and billing equity update event streams.

### Host Profile & Docker Runtime Decision ⚠️ REQUIRED

> **YOU MUST CHOOSE HOST TYPE FOR EACH EXECUTABLE PROJECT** (`src/Api` and/or `src/Endpoint.In`) before implementation.

#### Host Type Matrix (enforced)

- **HTTP API host** (`src/Api/Program.cs`)
  - Logging packages: `Serilog.AspNetCore` (+ sinks/settings as needed)
  - Docker runtime base: `mcr.microsoft.com/dotnet/aspnet:10.0`
- **NServiceBus worker host** (`src/Endpoint.In/Program.cs`)
  - Logging packages: `Serilog.Extensions.Hosting` + `Serilog.Settings.Configuration` (+ sinks)
  - Docker runtime base: `mcr.microsoft.com/dotnet/runtime:10.0`

**Compatibility rule**: `Serilog.AspNetCore` requires ASP.NET shared framework and therefore MUST NOT be paired with `dotnet/runtime` image.

**DECISION**:
- `Api` host profile: API
- `Endpoint.In` host profile: Worker
- `Api` Docker runtime base: aspnet:10.0
- `Endpoint.In` Docker runtime base: runtime:10.0
- Logging package set chosen per host: `Serilog.AspNetCore` (Api), `Serilog.Extensions.Hosting` + `Serilog.Settings.Configuration` (Endpoint.In)

## Constitution Check (RiskInsure Principles)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Reference**: [.specify/memory/constitution.md](../../../../../.specify/memory/constitution.md)

### Core Principle Compliance

- [x] **I. Domain Language Consistency** - Uses policy lifecycle domain terms (`Bound`, `Active`, `PendingCancellation`, `PendingRenewal`, `Expired`, `Renewed`)
- [x] **II. Data Model Strategy** - Cosmos DB chosen and documented with `/policyId` partition-local state transitions
- [x] **III. Atomic State Transitions** - Saga transition writes and policy projection updates remain partition-local and concurrency-safe
- [x] **IV. Idempotent Message Handling** - Message metadata and `(PolicyTermId, transition)` dedup strategy defined
- [x] **V. Structured Observability** - Correlation fields include `PolicyId`, `PolicyTermId`, `MessageId`, operation names
- [x] **VI. Message-Based Integration** - Billing input and lifecycle outputs are event/message-based
- [x] **VII. Thin Message Handlers** - Handlers and saga delegate business decisions to domain manager logic
- [x] **VIII. Repository Pattern** - Existing Policy repository abstraction is retained and extended
- [x] **IX. Saga Workflow Orchestration** - Saga orchestrates state progression and emits follow-up messages only
- [x] **X. Test Coverage Requirements** - Unit/saga/integration coverage planned with threshold targets
- [x] **XI. Technology Constraints** - .NET 10 + NServiceBus 9.x + Cosmos align with approved stack
- [x] **XII. Naming Conventions** - Command/event/saga naming follows constitutional conventions

### Platform Compatibility Gate

- [x] **Host/Runtime Compatibility** - Logging package choice matches Docker runtime base for each executable host

### Violations Requiring Justification

> Fill ONLY if any principle above cannot be satisfied

| Principle Violated | Why Required | Mitigation |
|-------------------|--------------|------------|
| [e.g., Cross-service HTTP] | [specific reason] | [how risk is managed] |

## Project Structure

### Documentation (this feature)

```text
services/policy/docs/specs/001-policy-lifecycle-workflow/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (RiskInsure Service Structure)

**Service Root**: `services/policy/`

```text
services/policy/
├── src/
│   ├── Api/
│   │   ├── Controllers/
│   │   ├── Models/
│   │   ├── Program.cs
│   │   └── appsettings.*.json
│   ├── Domain/
│   │   ├── Contracts/{Commands,Events}/
│   │   ├── DTOs/
│   │   ├── Managers/
│   │   ├── Models/
│   │   └── Services/
│   ├── Infrastructure/
│   │   ├── CosmosDbInitializer.cs
│   │   ├── CosmosSystemTextJsonSerializer.cs
│   │   └── NServiceBusConfigurationExtensions.cs
│   └── Endpoint.In/
│       ├── Handlers/
│       ├── Program.cs
│       └── appsettings.*.json
├── test/
│   ├── Unit.Tests/                  # xUnit unit tests
│   └── Integration.Tests/           # Playwright/API integration tests
│       ├── package.json
│       ├── playwright.config.ts
│       └── tests/
└── docs/
  ├── business/
  ├── technical/
  └── specs/
```

**Public Contracts** (if cross-service events):
```text
platform/RiskInsure.PublicContracts/
├── Events/          # Cross-service events
├── Commands/        # Cross-service commands
└── POCOs/          # Shared data objects
```

**Structure Decision**: MUST be concrete (no placeholders). Confirm exact service path and any cross-service contract paths.

Confirmed concrete paths:
- Feature artifacts: `services/policy/docs/specs/001-policy-lifecycle-workflow/`
- Service root: `services/policy/`
- Cross-service contract candidate path (if needed): `platform/RiskInsure.PublicContracts/Events/`

## Phase 0 Research Summary

- Lifecycle milestone strategy: percentage-based of total ticks (not fixed tick numbers)
- Renewal progression reference: open renewal workflow at configurable percentage and support optional reminder milestone before term end
- Billing equity cancellation model: threshold breach enters `PendingCancellation`; final cancel only after grace-window recheck
- Accepted default values for initial implementation: cancellation threshold `-20%`, grace window `10%` of term ticks

## Phase 1 Design Summary

- Saga correlation key: `PolicyTermId` across start and all advance messages
- Saga state: term dates, status flags, milestone percentages, equity/cancellation fields, completion metadata
- New design artifacts created: `research.md`, `data-model.md`, `quickstart.md`, `contracts/openapi.yaml`, `contracts/messaging-contracts.md`
- Post-design constitution check: PASS (no unresolved principle violations)

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |

