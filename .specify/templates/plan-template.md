# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/[FEATURE_SPEC_REL]`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

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

**DECISION**: [MUST BE FILLED - state "Cosmos DB" or "PostgreSQL" with brief rationale]  
**Rationale**: [Why this choice fits the feature's query/consistency/scale needs]

### Project Structure Constraints

**Bounded Context**: [Which service - e.g., Billing, Policy, FileIntegration]  
**Service Location**: `[SERVICE_ROOT_REL]/`  
**Layer Requirements** (per `copilot-instructions/project-structure.md` and existing service patterns like `services/billing/`):
- `src/Domain/` - Contracts, DTOs, managers, models, services (business logic + orchestration)
- `src/Infrastructure/` - Shared infrastructure configuration (Cosmos init, NServiceBus config extensions)
- `src/Api/` - HTTP endpoints/controllers/models + Program/appsettings
- `src/Endpoint.In/` - NServiceBus message processing host + Handlers
- `test/Unit.Tests/` - xUnit unit tests
- `test/Integration.Tests/` - Playwright/API integration tests (Node.js/npm)

**Performance Goals**: [e.g., Message processing <500ms p95, API responses <200ms p95]  
**Constraints**: [e.g., Idempotent handlers, thin message handlers, atomic state transitions]  
**Scale/Scope**: [e.g., 10K messages/hour, 100 concurrent users]

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
- `Api` host profile: [API | N/A]
- `Endpoint.In` host profile: [Worker | N/A]
- `Api` Docker runtime base: [aspnet:10.0 | N/A]
- `Endpoint.In` Docker runtime base: [runtime:10.0 | N/A]
- Logging package set chosen per host: [list actual package references]

## Constitution Check (RiskInsure Principles)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Reference**: [.specify/memory/constitution.md]([CONSTITUTION_REL])

### Core Principle Compliance

- [ ] **I. Domain Language Consistency** - Uses ubiquitous language from domain docs, no prohibited terms
- [ ] **II. Single-Partition Data Model** - Partition key identified (Cosmos) OR schema normalized (PostgreSQL)
- [ ] **III. Atomic State Transitions** - Aggregates updated atomically with ETags (Cosmos) or transactions (PostgreSQL)
- [ ] **IV. Idempotent Message Handlers** - Handlers check existing state before creating, safe to retry
- [ ] **V. Structured Observability** - All logs include correlation IDs (fileRunId/orderId/customerId + MessageId)
- [ ] **VI. Message-Based Integration** - Cross-service via Service Bus (no direct HTTP calls)
- [ ] **VII. Thin Message Handlers** - Handlers delegate to domain services/managers, no business logic in handlers
- [ ] **VIII. Test Coverage Requirements** - Domain 90%+, Application 80%+ (unit + integration)
- [ ] **IX. Technology Constraints** - Follows approved stack (.NET 10, NServiceBus 9.x, approved persistence)
- [ ] **X. Host/Runtime Compatibility** - Logging package choice matches Docker runtime base for each executable host

### Violations Requiring Justification

> Fill ONLY if any principle above cannot be satisfied

| Principle Violated | Why Required | Mitigation |
|-------------------|--------------|------------|
| [e.g., Cross-service HTTP] | [specific reason] | [how risk is managed] |

## Project Structure

### Documentation (this feature)

```text
[FEATURE_DIR_REL]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (RiskInsure Service Structure)

**Service Root**: `[SERVICE_ROOT_REL]/`

```text
[SERVICE_ROOT_REL]/
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

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
