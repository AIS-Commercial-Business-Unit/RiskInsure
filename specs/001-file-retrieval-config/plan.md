# Implementation Plan: Client File Retrieval Configuration

**Branch**: `001-file-retrieval-config` | **Date**: 2025-01-24 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-file-retrieval-config/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Build a client file retrieval configuration system that enables the RiskInsure platform to automatically check client file locations (FTP, HTTPS, Azure Blob Storage) on scheduled intervals, detect files matching configured patterns with date-based tokens, and trigger workflow orchestration events/commands when files are discovered. This feature integrates with the existing Distributed Workflow Orchestration Platform (specs/001-workflow-orchestration/) as a new bounded context, using the same messaging infrastructure (Azure Service Bus, NServiceBus), multi-tenancy patterns (client-scoped data access), and architectural principles (message-based integration, repository pattern, idempotent handlers).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0 with nullable reference types enabled  
**Primary Dependencies**: 
- NServiceBus 9.x+ with Azure Service Bus transport (messaging, sagas for schedule coordination)
- Azure Cosmos DB SDK (FileRetrievalConfiguration storage, execution history)
- Azure Service Bus (message transport - reuse from workflow platform)
- Quartz.NET or NCrontab (NEEDS CLARIFICATION - schedule evaluation and execution)
- Protocol-specific libraries: NEEDS CLARIFICATION (FTP client library, Azure.Storage.Blobs SDK, HttpClient)
- Azure Application Insights (observability)
- xUnit (testing framework)

**Storage**: 
- Azure Cosmos DB (FileRetrievalConfiguration, FileRetrievalExecution history, DiscoveredFile tracking)
- Partition strategy: NEEDS CLARIFICATION (by clientId for multi-tenant isolation, or hierarchical /clientId/configId)
- Document types: FileRetrievalConfiguration, FileRetrievalExecution, DiscoveredFile
- TTL for execution history: 90 days (spec assumption)

**Testing**: 
- xUnit for unit and integration tests
- Domain layer: 90%+ coverage target (configuration entities, token replacement logic)
- Application layer: 80%+ coverage target (services, message handlers, schedulers)
- Integration tests for protocol adapters (FTP, HTTPS, Azure Blob Storage) using test doubles/containers
- AAA pattern (Arrange-Act-Assert)

**Target Platform**: 
- Azure Container Apps (Linux containers)
- Docker-based deployment
- Background worker service for scheduled checks
- REST API for CRUD operations on FileRetrievalConfiguration

**Project Type**: Platform service (new bounded context: "File Retrieval")
- Separate domain within RiskInsure ecosystem
- Integrates with workflow orchestration platform via Azure Service Bus messages
- Exposes REST API for configuration management

**Performance Goals**:
- Support 100+ FileRetrievalConfigurations per client without degradation
- Execute scheduled checks within 1 minute of scheduled time (99% of executions) - per SC-002
- File discovery to event/command publish latency < 5 seconds - per SC-003
- Support 100 concurrent file checks without performance degradation - per SC-004
- Date token replacement accuracy: 100% - per SC-008

**Constraints**:
- Message-based integration only (no direct HTTP to workflow platform)
- No distributed transactions (eventual consistency model)
- All message handlers must be idempotent (at-least-once delivery)
- Zero duplicate workflow triggers for same file - per SC-007 (idempotency)
- Client-scoped security trimming: 100% enforcement - per SC-009
- Support extensible protocol architecture (design for future protocols)
- File detection only (no file download/storage in this feature)

**Scale/Scope**:
- Support 1,000+ FileRetrievalConfigurations across all clients
- Multiple configurations per client (typical: 3-5, max: 20 per spec story 4)
- Execution history retention: 90 days (per spec assumptions)
- 3 protocols initially: FTP, HTTPS, Azure Blob Storage (extensible for others)
- Multi-tenant: client isolation via clientId partition key

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Initial Check (Before Phase 0) - ✅ PASSED

All 10 principles passed initial validation. See results below.

### Post-Design Check (After Phase 1) - ✅ PASSED

**Re-evaluation Date**: 2025-01-24  
**Status**: All principles remain compliant after completing data model, contracts, and quickstart.

**Changes Since Initial Check**: None - design aligns with initial architectural decisions.

---

### Principle I: Domain Language Consistency
**Status**: ✅ **PASS** (with action required)  
**Assessment**: File retrieval configuration introduces domain terminology that must be documented consistently.  
**Required Actions**:
- Document file retrieval domain glossary in `services/file-retrieval/docs/file-retrieval-standards.md`
- Define terms: FileRetrievalConfiguration, Protocol, Schedule, Token, DiscoveredFile, FileCheck, ConfigurationExecution
- Ensure consistent usage across code, messages, and documentation
- Align with workflow orchestration terminology (events, commands)

**Post-Design Validation**: ✅ Data model, contracts, and quickstart use consistent terminology throughout.

### Principle II: Single-Partition Data Model
**Status**: ✅ **PASS**  
**Assessment**: File retrieval configurations will use single-partition model in Cosmos DB.  
**Design**:
- One container for file retrieval documents
- Partition key: `/clientId` for tenant isolation and free queries within client
- Document types: FileRetrievalConfiguration, FileRetrievalExecution, DiscoveredFile
- Type discriminator field to distinguish entity types
- Cross-partition queries only for admin/reporting dashboards

**Post-Design Validation**: ✅ Data model confirms 3 containers with appropriate partition strategies:
- `file-retrieval-configurations`: Partition key `/clientId`
- `file-retrieval-executions`: Partition key `/clientId/configurationId` (hierarchical)
- `discovered-files`: Partition key `/clientId/configurationId` (hierarchical) + unique key constraint

### Principle III: Atomic State Transitions
**Status**: ✅ **PASS**  
**Assessment**: Configuration updates will use optimistic concurrency (ETags) for atomicity.  
**Design**:
- All FileRetrievalConfiguration updates include ETag checks
- Execution status updates are atomic within partition
- Retry on conflict with exponential backoff
- Log all state transitions with before/after state
- File discovery tracking updates are atomic (prevent duplicate workflow triggers)

**Post-Design Validation**: ✅ Data model includes:
- ETag fields on all mutable entities (FileRetrievalConfiguration)
- State transition matrices for FileRetrievalExecution and DiscoveredFile
- Invariants enforce terminal states and required fields
- Commands include ETag for Update/Delete operations (optimistic concurrency)

### Principle IV: Idempotent Message Handlers
**Status**: ✅ **PASS** (critical for reliable file checks)  
**Assessment**: All message handlers MUST be idempotent to ensure zero duplicate workflow triggers (SC-007).  
**Design**:
- Check for existing DiscoveredFile records before publishing events
- Use file location + timestamp as idempotency key
- Handle duplicate file checks gracefully (return early if already processed)
- Support at-least-once delivery semantics
- Use outbox pattern for exactly-once processing of file discovery events
- Track processed files to prevent duplicate workflow triggers

**Post-Design Validation**: ✅ Idempotency enforced at multiple levels:
- DiscoveredFile has unique key constraint: `(clientId, configurationId, fileUrl, discoveryDate)`
- All commands include IdempotencyKey property
- All events include IdempotencyKey property
- Commands document idempotency checks in handler responsibilities
- Research.md section 6 details idempotency strategy with ETag-based atomicity

### Principle V: Structured Observability
**Status**: ✅ **PASS**  
**Assessment**: Comprehensive observability required for troubleshooting file retrieval issues (User Story 6).  
**Design**:
- Include `clientId`, `configurationId`, `executionId`, `protocol` in all logs
- Correlation ID from scheduled execution or API request
- Structured logging for all file check events
- Information level: configuration changes, file discoveries, successful checks
- Warning level: connection retries, file not found (if expected), token replacement warnings
- Error level: connection failures, authentication errors, configuration errors
- Metrics: check duration, success rate, files discovered, protocol-specific errors

**Post-Design Validation**: ✅ Observability designed throughout:
- All commands/events include CorrelationId, ClientId, ConfigurationId
- FileCheckFailed event includes ErrorCategory for categorized monitoring
- FileCheckCompleted event includes DurationMs, FilesFound, FilesProcessed for metrics
- Research.md section 8 defines logging standards, error categories, and metrics

### Principle VI: Message-Based Integration
**Status**: ✅ **PASS** (foundational requirement)  
**Assessment**: File retrieval integrates with workflow platform via Azure Service Bus messages.  
**Design**:
- Events: `FileDiscovered`, `FileCheckCompleted`, `FileCheckFailed`, `ConfigurationUpdated`
- Commands: `ExecuteFileCheck`, `ProcessDiscoveredFile` (sent to workflow platform)
- Use NServiceBus `context.Publish()` for file discovery events
- Use NServiceBus `context.Send()` for commands to workflow platform
- All messages include standard metadata (MessageId, OccurredUtc, clientId, configurationId, IdempotencyKey)
- No direct HTTP calls to workflow platform

**Post-Design Validation**: ✅ Contracts fully defined:
- 5 commands: ExecuteFileCheck, CreateConfiguration, UpdateConfiguration, DeleteConfiguration, ProcessDiscoveredFile
- 6 events: FileDiscovered, FileCheckCompleted, FileCheckFailed, ConfigurationCreated, ConfigurationUpdated, ConfigurationDeleted
- All messages use imperative (commands) or past-tense (events) naming
- All messages include standard metadata fields
- NServiceBus routing documented in contracts

### Principle VII: Thin Message Handlers
**Status**: ✅ **PASS**  
**Assessment**: Handlers will delegate to file retrieval services.  
**Design**:
- Handlers validate message structure only
- Delegate to `IFileCheckService`, `IConfigurationService`, `IProtocolAdapter`
- Handlers publish resulting events
- No business logic in handler classes
- Example: `ExecuteFileCheckHandler` → validates → calls `fileCheckService.ExecuteCheck()` → publishes `FileDiscovered` events

**Post-Design Validation**: ✅ Contracts document handler responsibilities:
- Each command contract specifies: "Handled By: {Handler} → {Service}.{Method}()"
- Handler responsibilities limited to: validate, delegate, publish
- Business logic clearly delegated to services (FileCheckService, ConfigurationService, TokenReplacementService)
- Project structure separates handlers (Application layer) from services (Application layer) and domain logic (Domain layer)

### Principle VIII: Test Coverage Requirements
**Status**: ✅ **PASS**  
**Assessment**: Test coverage targets align with constitution.  
**Commitment**:
- Domain layer (configuration entities, token logic, protocol abstractions): 90%+ coverage
- Application layer (services, handlers, schedulers): 80%+ coverage
- Integration tests for each protocol adapter (FTP, HTTPS, Azure Blob)
- Integration tests for idempotency (duplicate file checks)
- Contract tests for API endpoints (CRUD operations)
- Test token replacement logic thoroughly (edge cases, invalid dates)

**Post-Design Validation**: ✅ Test strategy documented:
- Quickstart.md includes test commands and coverage targets
- Project structure includes 3 test projects: Domain.Tests, Application.Tests, Integration.Tests
- Quickstart.md documents protocol-specific testing with test servers (Docker, json-server, Azurite)
- Research.md section 5 includes comprehensive test cases for token replacement (100% accuracy requirement)

### Principle IX: Technology Constraints
**Status**: ✅ **PASS**  
**Assessment**: All technology choices align with approved stack.  
**Compliance**:
- ✅ .NET 10.0 with C# 13
- ✅ Azure Cosmos DB for storage
- ✅ Azure Service Bus + NServiceBus for messaging
- ✅ Azure Container Apps for hosting
- ✅ xUnit for testing
- ✅ NO Entity Framework Core (use repository pattern with Cosmos SDK)
- ✅ NO distributed transactions (eventual consistency)

**Post-Design Validation**: ✅ All research decisions comply:
- NCrontab (lightweight cron library) - compliant
- FluentFTP (FTP client library) - compliant (MIT license, no EF Core)
- HttpClient + IHttpClientFactory - built-in .NET, compliant
- Azure.Storage.Blobs SDK - official Azure SDK, compliant
- Repository pattern enforced in project structure (Domain defines interfaces, Infrastructure implements)
- No Azure Functions (constitution prohibits) - using HostedService in Container Apps

### Principle X: Naming Conventions
**Status**: ✅ **PASS**  
**Assessment**: Naming follows strict conventions.  
**Commitment**:
- Commands: `ExecuteFileCheck`, `UpdateConfiguration`, `DeleteConfiguration`
- Events: `FileDiscovered`, `FileCheckCompleted`, `ConfigurationCreated`, `ConfigurationDeleted`
- Services: `FileCheckService`, `ConfigurationService`, `ProtocolAdapterFactory`
- Repositories: `IFileRetrievalConfigurationRepository`, `IFileRetrievalExecutionRepository`
- Handlers: `ExecuteFileCheckHandler`, `FileDiscoveredHandler`
- Avoid abbreviations: use `Configuration` not `Config`, `Discovered` not `Found`

**Post-Design Validation**: ✅ All contracts follow naming conventions:
- Commands: ExecuteFileCheck, CreateConfiguration, UpdateConfiguration, DeleteConfiguration, ProcessDiscoveredFile (all imperative)
- Events: FileDiscovered, FileCheckCompleted, FileCheckFailed, ConfigurationCreated, ConfigurationUpdated, ConfigurationDeleted (all past-tense)
- Services/Repositories follow standard naming in project structure
- Entities use full names (FileRetrievalConfiguration, not FileRetrievalConfig)

---

**Overall Status**: ✅ **ALL PRINCIPLES PASS (RE-VALIDATED POST-DESIGN)**  
**Gate Decision**: **APPROVED FOR PHASE 2 (TASK GENERATION)**

No constitutional violations. All principles compliant. Design artifacts (data model, contracts, quickstart) align with RiskInsure architectural standards.

## Project Structure

### Documentation (this feature)

```text
specs/001-file-retrieval-config/
├── spec.md              # Feature specification (complete)
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command - GENERATED BELOW)
├── data-model.md        # Phase 1 output (/speckit.plan command - GENERATED BELOW)
├── quickstart.md        # Phase 1 output (/speckit.plan command - GENERATED BELOW)
├── contracts/           # Phase 1 output (/speckit.plan command - GENERATED BELOW)
│   ├── commands.md      # Command message contracts
│   └── events.md        # Event message contracts
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
services/
├── file-retrieval/                        # NEW bounded context for this feature
│   ├── FileRetrieval.Domain/              # Domain layer (entities, value objects)
│   │   ├── Entities/
│   │   │   ├── FileRetrievalConfiguration.cs
│   │   │   ├── FileRetrievalExecution.cs
│   │   │   └── DiscoveredFile.cs
│   │   ├── ValueObjects/
│   │   │   ├── ScheduleDefinition.cs
│   │   │   ├── ProtocolSettings.cs
│   │   │   └── FilePattern.cs
│   │   ├── Enums/
│   │   │   ├── ProtocolType.cs
│   │   │   ├── ExecutionStatus.cs
│   │   │   └── DiscoveryStatus.cs
│   │   └── Repositories/                  # Repository interfaces
│   │       ├── IFileRetrievalConfigurationRepository.cs
│   │       ├── IFileRetrievalExecutionRepository.cs
│   │       └── IDiscoveredFileRepository.cs
│   │
│   ├── FileRetrieval.Application/          # Application layer (services, handlers)
│   │   ├── Services/
│   │   │   ├── FileCheckService.cs         # Orchestrates file check execution
│   │   │   ├── ConfigurationService.cs     # CRUD operations for configurations
│   │   │   ├── TokenReplacementService.cs  # Date token replacement logic
│   │   │   └── ScheduleExecutionService.cs # Schedule evaluation and triggering
│   │   ├── Protocols/                      # Protocol abstraction
│   │   │   ├── IProtocolAdapter.cs         # Interface for protocol implementations
│   │   │   ├── ProtocolAdapterFactory.cs   # Factory for adapter selection
│   │   │   ├── FtpProtocolAdapter.cs       # FTP implementation
│   │   │   ├── HttpsProtocolAdapter.cs     # HTTPS implementation
│   │   │   └── AzureBlobProtocolAdapter.cs # Azure Blob Storage implementation
│   │   ├── MessageHandlers/                # NServiceBus message handlers (thin)
│   │   │   ├── ExecuteFileCheckHandler.cs
│   │   │   ├── ConfigurationCreatedHandler.cs
│   │   │   └── ConfigurationDeletedHandler.cs
│   │   └── Queries/                        # Query models for API
│   │       ├── GetConfigurationQuery.cs
│   │       └── GetExecutionHistoryQuery.cs
│   │
│   ├── FileRetrieval.Infrastructure/        # Infrastructure layer (data access, external services)
│   │   ├── Repositories/                    # Cosmos DB implementations
│   │   │   ├── FileRetrievalConfigurationRepository.cs
│   │   │   ├── FileRetrievalExecutionRepository.cs
│   │   │   └── DiscoveredFileRepository.cs
│   │   ├── Cosmos/                          # Cosmos DB helpers
│   │   │   ├── CosmosDbContext.cs
│   │   │   └── DocumentMappings.cs
│   │   └── Scheduling/                      # Schedule execution infrastructure
│   │       ├── SchedulerHostedService.cs    # Background service for schedule checks
│   │       └── ScheduleEvaluator.cs         # Cron/schedule evaluation logic
│   │
│   ├── FileRetrieval.API/                   # REST API for configuration management
│   │   ├── Controllers/
│   │   │   ├── ConfigurationController.cs   # CRUD endpoints
│   │   │   └── ExecutionHistoryController.cs # Query execution history
│   │   ├── Models/                          # DTOs for API
│   │   │   ├── CreateConfigurationRequest.cs
│   │   │   ├── UpdateConfigurationRequest.cs
│   │   │   └── ConfigurationResponse.cs
│   │   ├── Validators/                      # FluentValidation validators
│   │   │   └── ConfigurationRequestValidator.cs
│   │   └── Program.cs                       # API startup
│   │
│   ├── FileRetrieval.Worker/                # Background worker for scheduled checks
│   │   ├── Program.cs                       # Worker startup
│   │   └── ScheduledCheckWorker.cs          # Hosted service for executing checks
│   │
│   ├── FileRetrieval.Contracts/             # Shared message contracts
│   │   ├── Commands/
│   │   │   ├── ExecuteFileCheck.cs
│   │   │   └── ProcessDiscoveredFile.cs
│   │   └── Events/
│   │       ├── FileDiscovered.cs
│   │       ├── FileCheckCompleted.cs
│   │       ├── FileCheckFailed.cs
│   │       └── ConfigurationCreated.cs
│   │
│   └── docs/
│       └── file-retrieval-standards.md      # Domain terminology and standards
│
└── workflow/                                # EXISTING workflow orchestration platform
    └── (integration point via Service Bus messages)

tests/
├── FileRetrieval.Domain.Tests/              # Domain unit tests (90%+ coverage)
│   ├── Entities/
│   ├── ValueObjects/
│   └── TokenReplacementTests.cs             # Comprehensive token logic tests
│
├── FileRetrieval.Application.Tests/         # Application unit tests (80%+ coverage)
│   ├── Services/
│   ├── Protocols/
│   │   ├── FtpProtocolAdapterTests.cs
│   │   ├── HttpsProtocolAdapterTests.cs
│   │   └── AzureBlobProtocolAdapterTests.cs
│   └── MessageHandlers/
│
└── FileRetrieval.Integration.Tests/         # Integration tests
    ├── RepositoryTests.cs                   # Cosmos DB integration
    ├── ProtocolAdapterIntegrationTests.cs   # Real protocol tests (test containers)
    ├── IdempotencyTests.cs                  # Duplicate file check prevention
    └── ApiEndpointTests.cs                  # API contract tests
```

**Structure Decision**: New bounded context "File Retrieval" as a separate service under `services/file-retrieval/`. This aligns with the existing `services/workflow/` structure from the workflow orchestration platform. The feature is decomposed into standard layers:
- **Domain**: Business entities and rules (protocol-agnostic)
- **Application**: Use cases, services, message handlers (thin handlers, protocol adapters)
- **Infrastructure**: Data access (Cosmos DB repositories), scheduling, external integrations
- **API**: REST endpoints for CRUD operations (security-trimmed)
- **Worker**: Background hosted service for scheduled checks
- **Contracts**: Shared message definitions for NServiceBus integration

This structure supports:
- Clean separation of concerns (Domain → Application → Infrastructure)
- Repository pattern (Domain defines interfaces, Infrastructure implements)
- Extensible protocol architecture (IProtocolAdapter interface, factory pattern)
- Message-based integration (NServiceBus handlers)
- Multi-tenancy (client-scoped repositories)

## Complexity Tracking

> **Not Required** - No constitutional violations identified. All principles pass without justified exceptions.
