# Tasks: Client File Retrieval Configuration

**Input**: Design documents from `/specs/001-file-retrieval-config/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Tests are NOT explicitly requested in the specification. This task list focuses on implementation only.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Based on plan.md, the project uses:
- `services/file-retrieval/` as the root directory for this bounded context
- Layered architecture: Domain, Application, Infrastructure, API, Worker, Contracts
- Tests in `tests/` directory at repository root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create solution file services/file-retrieval/FileRetrieval.sln with all projects
- [X] T002 Create FileRetrieval.Domain project in services/file-retrieval/FileRetrieval.Domain/
- [X] T003 [P] Create FileRetrieval.Application project in services/file-retrieval/FileRetrieval.Application/
- [X] T004 [P] Create FileRetrieval.Infrastructure project in services/file-retrieval/FileRetrieval.Infrastructure/
- [X] T005 [P] Create FileRetrieval.API project in services/file-retrieval/FileRetrieval.API/
- [X] T006 [P] Create FileRetrieval.Worker project in services/file-retrieval/FileRetrieval.Worker/
- [X] T007 [P] Create FileRetrieval.Contracts project in services/file-retrieval/FileRetrieval.Contracts/
- [X] T008 Create test projects: tests/FileRetrieval.Domain.Tests/, tests/FileRetrieval.Application.Tests/, tests/FileRetrieval.Integration.Tests/
- [X] T009 Add NuGet packages for Domain: no external dependencies (pure domain logic)
- [X] T010 [P] Add NuGet packages for Application: NServiceBus 9.x, Microsoft.Extensions.DependencyInjection
- [X] T011 [P] Add NuGet packages for Infrastructure: Azure.Cosmos, NServiceBus.Azure, NCrontab, FluentFTP, Azure.Storage.Blobs, Azure.Identity
- [X] T012 [P] Add NuGet packages for API: ASP.NET Core 10.0, FluentValidation, NSwag for OpenAPI
- [X] T013 [P] Add NuGet packages for Worker: Microsoft.Extensions.Hosting, NServiceBus
- [X] T014 Configure project references: Application → Domain, Infrastructure → Domain + Application, API → Application + Infrastructure, Worker → Application + Infrastructure
- [X] T015 Create .editorconfig with C# 13 coding standards in services/file-retrieval/.editorconfig
- [X] T016 [P] Create appsettings.json templates for API and Worker with Cosmos DB, Service Bus, Key Vault configuration sections
- [X] T017 Create README.md in services/file-retrieval/README.md with project overview and links to specs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Domain Foundation

- [X] T018 Create ProtocolType enum in services/file-retrieval/FileRetrieval.Domain/Enums/ProtocolType.cs
- [X] T019 [P] Create ExecutionStatus enum in services/file-retrieval/FileRetrieval.Domain/Enums/ExecutionStatus.cs
- [X] T020 [P] Create DiscoveryStatus enum in services/file-retrieval/FileRetrieval.Domain/Enums/DiscoveryStatus.cs
- [X] T021 [P] Create AuthType enum in services/file-retrieval/FileRetrieval.Domain/Enums/AuthType.cs
- [X] T022 [P] Create AzureAuthType enum in services/file-retrieval/FileRetrieval.Domain/Enums/AzureAuthType.cs
- [X] T023 Create base ProtocolSettings abstract class in services/file-retrieval/FileRetrieval.Domain/ValueObjects/ProtocolSettings.cs
- [X] T024 [P] Create FtpProtocolSettings value object in services/file-retrieval/FileRetrieval.Domain/ValueObjects/FtpProtocolSettings.cs
- [X] T025 [P] Create HttpsProtocolSettings value object in services/file-retrieval/FileRetrieval.Domain/ValueObjects/HttpsProtocolSettings.cs
- [X] T026 [P] Create AzureBlobProtocolSettings value object in services/file-retrieval/FileRetrieval.Domain/ValueObjects/AzureBlobProtocolSettings.cs
- [X] T027 Create ScheduleDefinition value object in services/file-retrieval/FileRetrieval.Domain/ValueObjects/ScheduleDefinition.cs
- [X] T028 [P] Create EventDefinition value object in services/file-retrieval/FileRetrieval.Domain/ValueObjects/EventDefinition.cs
- [X] T029 [P] Create CommandDefinition value object in services/file-retrieval/FileRetrieval.Domain/ValueObjects/CommandDefinition.cs
- [X] T030 Create FilePattern value object in services/file-retrieval/FileRetrieval.Domain/ValueObjects/FilePattern.cs

### Repository Interfaces (Domain)

- [X] T031 Create IFileRetrievalConfigurationRepository interface in services/file-retrieval/FileRetrieval.Domain/Repositories/IFileRetrievalConfigurationRepository.cs
- [X] T032 [P] Create IFileRetrievalExecutionRepository interface in services/file-retrieval/FileRetrieval.Domain/Repositories/IFileRetrievalExecutionRepository.cs
- [X] T033 [P] Create IDiscoveredFileRepository interface in services/file-retrieval/FileRetrieval.Domain/Repositories/IDiscoveredFileRepository.cs

### Infrastructure Foundation

- [X] T034 Create CosmosDbContext in services/file-retrieval/FileRetrieval.Infrastructure/Cosmos/CosmosDbContext.cs with container initialization logic
- [X] T035 Create database setup script in services/file-retrieval/scripts/DatabaseSetup/ to create Cosmos DB containers with partition keys and unique constraints
- [X] T036 Implement FileRetrievalConfigurationRepository in services/file-retrieval/FileRetrieval.Infrastructure/Repositories/FileRetrievalConfigurationRepository.cs
- [X] T037 [P] Implement FileRetrievalExecutionRepository in services/file-retrieval/FileRetrieval.Infrastructure/Repositories/FileRetrievalExecutionRepository.cs
- [X] T038 [P] Implement DiscoveredFileRepository in services/file-retrieval/FileRetrieval.Infrastructure/Repositories/DiscoveredFileRepository.cs
- [X] T039 Create Azure Key Vault client wrapper in services/file-retrieval/FileRetrieval.Infrastructure/KeyVault/KeyVaultSecretClient.cs
- [X] T040 Configure dependency injection for repositories and Key Vault in services/file-retrieval/FileRetrieval.Infrastructure/DependencyInjection.cs

### Application Foundation

- [X] T041 Create TokenReplacementService in services/file-retrieval/FileRetrieval.Application/Services/TokenReplacementService.cs
- [X] T042 Create IProtocolAdapter interface in services/file-retrieval/FileRetrieval.Application/Protocols/IProtocolAdapter.cs
- [X] T043 Create ProtocolAdapterFactory in services/file-retrieval/FileRetrieval.Application/Protocols/ProtocolAdapterFactory.cs
- [X] T044 Create DiscoveredFileInfo record in services/file-retrieval/FileRetrieval.Application/Protocols/DiscoveredFileInfo.cs

### Message Contracts

- [X] T045 Create ExecuteFileCheck command in services/file-retrieval/FileRetrieval.Contracts/Commands/ExecuteFileCheck.cs
- [X] T046 [P] Create CreateConfiguration command in services/file-retrieval/FileRetrieval.Contracts/Commands/CreateConfiguration.cs
- [X] T047 [P] Create UpdateConfiguration command in services/file-retrieval/FileRetrieval.Contracts/Commands/UpdateConfiguration.cs
- [X] T048 [P] Create DeleteConfiguration command in services/file-retrieval/FileRetrieval.Contracts/Commands/DeleteConfiguration.cs
- [X] T049 [P] Create ProcessDiscoveredFile command in services/file-retrieval/FileRetrieval.Contracts/Commands/ProcessDiscoveredFile.cs
- [X] T050 Create FileDiscovered event in services/file-retrieval/FileRetrieval.Contracts/Events/FileDiscovered.cs
- [X] T051 [P] Create FileCheckCompleted event in services/file-retrieval/FileRetrieval.Contracts/Events/FileCheckCompleted.cs
- [X] T052 [P] Create FileCheckFailed event in services/file-retrieval/FileRetrieval.Contracts/Events/FileCheckFailed.cs
- [X] T053 [P] Create ConfigurationCreated event in services/file-retrieval/FileRetrieval.Contracts/Events/ConfigurationCreated.cs
- [X] T054 [P] Create ConfigurationUpdated event in services/file-retrieval/FileRetrieval.Contracts/Events/ConfigurationUpdated.cs
- [X] T055 [P] Create ConfigurationDeleted event in services/file-retrieval/FileRetrieval.Contracts/Events/ConfigurationDeleted.cs
- [X] T056 Create supporting DTOs (ScheduleDefinitionDto, EventDefinitionDto, CommandDefinitionDto) in services/file-retrieval/FileRetrieval.Contracts/DTOs/

### NServiceBus Configuration

- [X] T057 Configure NServiceBus endpoint for Worker in services/file-retrieval/FileRetrieval.Worker/Program.cs
- [X] T058 Configure NServiceBus endpoint for API in services/file-retrieval/FileRetrieval.API/Program.cs
- [X] T059 Configure message routing for commands and events in both endpoints

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Configure Basic File Retrieval (Priority: P1) 🎯 MVP

**Goal**: Enable system administrators to create FileRetrievalConfigurations with HTTPS protocol, date tokens, and schedules. Configurations are persisted and can be retrieved.

**Independent Test**: Create a FileRetrievalConfiguration through the API with HTTPS protocol, date tokens (`{yyyy}/{mm}/{dd}`), and daily schedule, then verify the configuration is persisted and can be retrieved. Tokens should be correctly replaced with current date values when evaluated.

### Implementation for User Story 1

- [X] T060 [P] [US1] Create FileRetrievalConfiguration entity in services/file-retrieval/FileRetrieval.Domain/Entities/FileRetrievalConfiguration.cs
- [X] T061 [P] [US1] Create FileRetrievalExecution entity in services/file-retrieval/FileRetrieval.Domain/Entities/FileRetrievalExecution.cs
- [X] T062 [P] [US1] Create DiscoveredFile entity in services/file-retrieval/FileRetrieval.Domain/Entities/DiscoveredFile.cs
- [X] T063 [US1] Implement ConfigurationService in services/file-retrieval/FileRetrieval.Application/Services/ConfigurationService.cs with CreateAsync, GetByIdAsync, GetByClientAsync methods
- [X] T064 [US1] Implement CreateConfigurationHandler in services/file-retrieval/FileRetrieval.Application/MessageHandlers/CreateConfigurationHandler.cs
- [X] T065 [US1] Implement ConfigurationController in services/file-retrieval/FileRetrieval.API/Controllers/ConfigurationController.cs with POST and GET endpoints
- [X] T066 [US1] Create CreateConfigurationRequest DTO in services/file-retrieval/FileRetrieval.API/Models/CreateConfigurationRequest.cs
- [X] T067 [US1] Create ConfigurationResponse DTO in services/file-retrieval/FileRetrieval.API/Models/ConfigurationResponse.cs
- [X] T068 [US1] Implement ConfigurationRequestValidator using FluentValidation in services/file-retrieval/FileRetrieval.API/Validators/ConfigurationRequestValidator.cs
- [X] T069 [US1] Add token replacement validation logic to TokenReplacementService (validate tokens not in server name)
- [X] T070 [US1] Add JWT authentication and claims-based authorization to API in services/file-retrieval/FileRetrieval.API/Program.cs
- [X] T071 [US1] Implement clientId extraction from JWT claims in ConfigurationController
- [X] T072 [US1] Add structured logging for configuration creation/retrieval operations with clientId, configurationId context
- [X] T073 [US1] Configure OpenAPI/Swagger documentation in services/file-retrieval/FileRetrieval.API/Program.cs

**Checkpoint**: At this point, User Story 1 should be fully functional - administrators can create and retrieve configurations via API with security trimming

---

## Phase 4: User Story 2 - Retrieve Files on Schedule (Priority: P1)

**Goal**: System automatically checks configured locations based on schedules. When scheduled time arrives, system connects to specified location (HTTPS, FTP, or Azure Blob), evaluates path/filename pattern with current date values, and determines if matching files exist.

**Independent Test**: Create a FileRetrievalConfiguration with a schedule, place a matching file at the configured location, advance/wait for scheduled time, and verify the system detects the file and creates a FileRetrievalExecution record.

### Implementation for User Story 2

- [X] T074 [P] [US2] Implement FtpProtocolAdapter in services/file-retrieval/FileRetrieval.Application/Protocols/FtpProtocolAdapter.cs using FluentFTP
- [X] T075 [P] [US2] Implement HttpsProtocolAdapter in services/file-retrieval/FileRetrieval.Application/Protocols/HttpsProtocolAdapter.cs using HttpClient
- [X] T076 [P] [US2] Implement AzureBlobProtocolAdapter in services/file-retrieval/FileRetrieval.Application/Protocols/AzureBlobProtocolAdapter.cs using Azure.Storage.Blobs
- [X] T077 [US2] Implement FileCheckService in services/file-retrieval/FileRetrieval.Application/Services/FileCheckService.cs with ExecuteCheck method
- [X] T078 [US2] Implement ExecuteFileCheckHandler in services/file-retrieval/FileRetrieval.Application/MessageHandlers/ExecuteFileCheckHandler.cs
- [X] T079 [US2] Implement ScheduleEvaluator in services/file-retrieval/FileRetrieval.Infrastructure/Scheduling/ScheduleEvaluator.cs using NCrontab for cron expression parsing
- [X] T080 [US2] Implement SchedulerHostedService in services/file-retrieval/FileRetrieval.Infrastructure/Scheduling/SchedulerHostedService.cs as BackgroundService
- [X] T081 [US2] Register SchedulerHostedService in Worker Program.cs
- [X] T082 [US2] Implement retry logic with exponential backoff in FileCheckService for transient errors
- [X] T083 [US2] Add protocol-specific error handling and categorization (AuthenticationFailure, ConnectionTimeout, ProtocolError)
- [X] T084 [US2] Add structured logging for file check execution with protocol, executionId, duration, filesFound metrics
- [X] T085 [US2] Implement connection pooling/reuse for FTP and HTTPS adapters

**Checkpoint**: At this point, User Story 2 should be fully functional - scheduled file checks execute automatically and detect files

---

## Phase 5: User Story 3 - Trigger Workflows on File Discovery (Priority: P1)

**Goal**: When system finds a file matching a FileRetrievalConfiguration, it executes configured actions: publishing events and/or sending commands to the workflow orchestration platform with file metadata (location, name, size).

**Independent Test**: Create a configuration with specific event/command actions, trigger file discovery (manually or via schedule), and verify the expected FileDiscovered events are published to Service Bus and ProcessDiscoveredFile commands are sent with correct file metadata.

### Implementation for User Story 3

- [X] T086 [US3] Implement file discovery event publishing logic in FileCheckService (check for duplicate DiscoveredFile before publishing)
- [X] T087 [US3] Implement idempotency check using DiscoveredFile repository (unique key constraint on clientId, configurationId, fileUrl, discoveryDate)
- [X] T088 [US3] Implement ProcessDiscoveredFile command sending logic in FileCheckService
- [X] T089 [US3] Add FileCheckCompleted event publishing after successful check in ExecuteFileCheckHandler
- [X] T090 [US3] Add FileCheckFailed event publishing after failed check in ExecuteFileCheckHandler
- [X] T091 [US3] Implement ConfigurationCreated event publishing in CreateConfigurationHandler
- [X] T092 [US3] Add validation that at least one EventDefinition exists in configuration during creation
- [X] T093 [US3] Add correlation ID propagation across commands and events for distributed tracing
- [X] T094 [US3] Add structured logging for event/command publishing with idempotency key, message type, destination endpoint
- [X] T095 [US3] Create integration test helpers for verifying message publishing in tests/FileRetrieval.Integration.Tests/MessageTestHelpers.cs

**Checkpoint**: At this point, User Story 3 should be fully functional - file discovery triggers workflow platform integration via events/commands with 100% idempotency

---

## Phase 6: User Story 4 - Manage Multiple Client Configurations (Priority: P2)

**Goal**: Support clients with multiple data feeds from different sources. Each FileRetrievalConfiguration operates independently with different protocols, schedules, and file patterns. System handles multiple configurations per client without interference.

**Independent Test**: Create 3 FileRetrievalConfigurations for one client with different protocols (FTP, HTTPS, Azure Blob) and schedules, simulate scheduled checks for each, and verify each configuration executes independently with correct protocol handling and no cross-configuration interference.

### Implementation for User Story 4

- [X] T096 [US4] Add pagination support to GetByClientAsync in ConfigurationService (support clients with 20+ configurations)
- [X] T097 [US4] Add filtering by protocol and isActive status to configuration queries in ConfigurationController
- [X] T098 [US4] Implement configuration listing endpoint with sorting and filtering in ConfigurationController
- [X] T099 [US4] Add secondary index on isActive and nextScheduledRun fields in Cosmos DB container setup
- [X] T100 [US4] Implement concurrent execution limiting in SchedulerHostedService (max 100 concurrent checks per SC-004)
- [X] T101 [US4] Add distributed locking mechanism using Cosmos DB lease to prevent duplicate scheduled checks across multiple worker instances
- [X] T102 [US4] Add metrics collection for active configurations per client, execution counts per protocol
- [X] T103 [US4] Add protocol-specific connection timeout and retry configuration per protocol adapter
- [X] T104 [US4] Implement graceful failure handling where one configuration failure does not affect others
- [X] T105 [US4] Add structured logging for multi-configuration scenarios with clear configuration isolation

**Checkpoint**: At this point, User Story 4 should be fully functional - clients can have 20+ configurations with different protocols executing independently

---

## Phase 7: User Story 5 - Update and Delete Configurations (Priority: P2)

**Goal**: Enable administrators to update existing FileRetrievalConfigurations (change protocol, schedule, file patterns) and delete configurations when no longer needed. Changes are persisted with optimistic concurrency control (ETag). Execution history is retained after deletion.

**Independent Test**: Create a configuration, update it with different protocol and schedule settings, verify changes are persisted and next execution uses new settings, then delete the configuration and confirm it no longer executes but execution history remains.

### Implementation for User Story 5

- [X] T106 [US5] Implement UpdateAsync method in ConfigurationService with ETag-based optimistic concurrency
- [X] T107 [US5] Implement UpdateConfigurationHandler in services/file-retrieval/FileRetrieval.Application/MessageHandlers/UpdateConfigurationHandler.cs
- [X] T108 [US5] Implement DeleteAsync method in ConfigurationService (soft delete by setting IsActive = false)
- [X] T109 [US5] Implement DeleteConfigurationHandler in services/file-retrieval/FileRetrieval.Application/MessageHandlers/DeleteConfigurationHandler.cs
- [X] T110 [US5] Add PUT endpoint to ConfigurationController for updates with ETag validation
- [X] T111 [US5] Add DELETE endpoint to ConfigurationController for soft delete with ETag validation
- [X] T112 [US5] Create UpdateConfigurationRequest DTO in services/file-retrieval/FileRetrieval.API/Models/UpdateConfigurationRequest.cs
- [X] T113 [US5] Implement ConfigurationUpdated event publishing in UpdateConfigurationHandler with ChangedFields tracking
- [X] T114 [US5] Implement ConfigurationDeleted event publishing in DeleteConfigurationHandler
- [X] T115 [US5] Add ETag conflict handling (409 Conflict response) with latest ETag returned to client
- [X] T116 [US5] Implement scheduler cache refresh when ConfigurationUpdated or ConfigurationDeleted events are published
- [X] T117 [US5] Add validation that updates do not break running executions (updates take effect on next scheduled run)
- [X] T118 [US5] Add structured logging for configuration updates and deletions with before/after state tracking

**Checkpoint**: At this point, User Story 5 should be fully functional - configurations have full lifecycle management with safe concurrent updates

---

## Phase 8: User Story 6 - Monitor Configuration Execution (Priority: P3)

**Goal**: Operations staff view execution history for each FileRetrievalConfiguration including: when checks were performed, whether files were found, which events/commands were triggered, and any errors encountered. This helps troubleshoot issues like missing files, connection failures, or misconfigured patterns.

**Independent Test**: Execute several scheduled checks (with both successes and failures), then query the execution history API and verify all execution attempts are logged with timestamps, outcomes (success/failure), file counts, and error details (if failed).

### Implementation for User Story 6

- [X] T119 [US6] Create ExecutionHistoryController in services/file-retrieval/FileRetrieval.API/Controllers/ExecutionHistoryController.cs
- [X] T120 [US6] Implement GetExecutionHistoryAsync method in ConfigurationService with date range filtering and pagination
- [X] T121 [US6] Implement GET endpoint for execution history by configuration ID in ExecutionHistoryController
- [X] T122 [US6] Create ExecutionHistoryResponse DTO in services/file-retrieval/FileRetrieval.API/Models/ExecutionHistoryResponse.cs
- [X] T123 [US6] Add filtering by status (Completed, Failed) and date range to execution history queries
- [X] T124 [US6] Implement GetExecutionDetailsAsync method to retrieve single execution with discovered files list
- [X] T125 [US6] Implement GET endpoint for single execution details in ExecutionHistoryController
- [X] T126 [US6] Add discovered files list to execution details response with file metadata
- [X] T127 [US6] Add error categorization display in execution history (AuthenticationFailure, ConnectionTimeout, etc.)
- [X] T128 [US6] Implement execution metrics aggregation endpoint (success rate, average duration, files discovered per day)
- [X] T129 [US6] Add Application Insights custom metrics tracking for FileCheckDuration, FileCheckSuccess, FilesDiscovered, ProtocolErrors
- [X] T130 [US6] Add Application Insights custom logging for all file check operations with correlation IDs
- [X] T131 [US6] Create monitoring dashboard queries documentation in services/file-retrieval/docs/monitoring.md
- [X] T132 [US6] Add OpenAPI documentation for all execution history endpoints

**Checkpoint**: At this point, User Story 6 should be fully functional - administrators can monitor execution history, view detailed execution records, and access aggregated metrics

---

## Phase 9: Polish & Cross-Cutting Concerns

**Goal**: Production hardening, documentation, performance testing, security improvements, and operational readiness

### Implementation for Polish Phase

- [X] T133 [P] Create file-retrieval-standards.md documentation in services/file-retrieval/docs/file-retrieval-standards.md with domain terminology glossary
- [X] T134 [P] Create deployment guide in services/file-retrieval/docs/deployment.md for Azure Container Apps
- [X] T135 [P] Create Docker Compose file for local development with Cosmos DB emulator, Azurite, test FTP server
- [X] T136 [P] Add health check endpoints to API and Worker for Azure Container Apps readiness probes
- [X] T137 Add performance testing for 100+ concurrent file checks (SC-004 validation)
- [X] T138 Add load testing for 1000+ configurations across all clients (scale validation)
- [X] T139 Implement rate limiting for API endpoints to prevent abuse
- [X] T140 Add comprehensive error handling middleware in API with structured error responses
- [X] T141 Add security headers (CORS, CSP, HSTS) to API responses
- [X] T142 Implement API versioning strategy (v1 prefix) in ConfigurationController and ExecutionHistoryController
- [X] T143 Add integration with Azure Application Insights for distributed tracing
- [X] T144 [P] Code review and refactoring for consistency with RiskInsure standards
- [X] T145 [P] Update quickstart.md validation steps based on implementation learnings
- [X] T146 Run all integration tests against real Azure resources (Cosmos DB, Service Bus, Azure Blob Storage)
- [X] T147 Verify constitution compliance: domain language consistency, single-partition queries, atomic state transitions, idempotent handlers
- [X] T148 Create runbook for common operational scenarios (configuration not executing, connection failures, file not found)

**Checkpoint**: At this point, User Story 6 should be fully functional - operations staff have full visibility into execution history and metrics

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T133 [P] Create file-retrieval-standards.md documentation in services/file-retrieval/docs/file-retrieval-standards.md with domain terminology glossary
- [ ] T134 [P] Create deployment guide in services/file-retrieval/docs/deployment.md for Azure Container Apps
- [ ] T135 [P] Create Docker Compose file for local development with Cosmos DB emulator, Azurite, test FTP server
- [ ] T136 [P] Add health check endpoints to API and Worker for Azure Container Apps readiness probes
- [ ] T137 Add performance testing for 100+ concurrent file checks (SC-004 validation)
- [ ] T138 Add load testing for 1000+ configurations across all clients (scale validation)
- [ ] T139 Implement rate limiting for API endpoints to prevent abuse
- [ ] T140 Add comprehensive error handling middleware in API with structured error responses
- [ ] T141 Add security headers (CORS, CSP, HSTS) to API responses
- [ ] T142 Implement API versioning strategy (v1 prefix) in ConfigurationController and ExecutionHistoryController
- [ ] T143 Add integration with Azure Application Insights for distributed tracing
- [ ] T144 [P] Code review and refactoring for consistency with RiskInsure standards
- [ ] T145 [P] Update quickstart.md validation steps based on implementation learnings
- [ ] T146 Run all integration tests against real Azure resources (Cosmos DB, Service Bus, Azure Blob Storage)
- [ ] T147 Verify constitution compliance: domain language consistency, single-partition queries, atomic state transitions, idempotent handlers
- [ ] T148 Create runbook for common operational scenarios (configuration not executing, connection failures, file not found)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-8)**: All depend on Foundational phase completion
  - US1 (Configure Basic File Retrieval): Can start after Foundational - No dependencies on other stories
  - US2 (Retrieve Files on Schedule): Can start after Foundational - Integrates with US1 entities but independently testable
  - US3 (Trigger Workflows on File Discovery): Depends on US2 (FileCheckService) - Extends US2 with event/command publishing
  - US4 (Manage Multiple Client Configurations): Can start after US1-US3 complete - Enhances existing functionality
  - US5 (Update and Delete Configurations): Depends on US1 (ConfigurationService) - Extends US1 with update/delete operations
  - US6 (Monitor Configuration Execution): Depends on US2 (FileRetrievalExecution entity) - Adds observability on top of existing execution tracking
- **Polish (Phase 9)**: Depends on all desired user stories being complete

### Critical Path (MVP: User Stories 1-3)

```
Setup → Foundational → US1 (Configure) → US2 (Schedule) → US3 (Trigger) → MVP Complete
```

### User Story Dependencies

- **User Story 1 (P1)**: Independent - can start after Foundational
- **User Story 2 (P1)**: Integrates with US1 entities (FileRetrievalConfiguration, FileRetrievalExecution) but independently testable
- **User Story 3 (P1)**: Extends US2 (FileCheckService) with event/command publishing - should complete after US2
- **User Story 4 (P2)**: Enhances US1-US3 with multi-configuration support - independently testable with existing infrastructure
- **User Story 5 (P2)**: Extends US1 (ConfigurationService) with update/delete - independently testable
- **User Story 6 (P3)**: Adds observability to US2 executions - independently testable with read-only queries

### Within Each User Story

- Entities before services
- Services before message handlers
- Message handlers before API controllers
- Domain logic before infrastructure implementation
- Core implementation before integration with other stories

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (different projects)
- All Foundational enum/value object tasks marked [P] can run in parallel (different files)
- Repository interfaces can run in parallel (T031-T033)
- Repository implementations can run in parallel (T036-T038)
- Protocol adapters can run in parallel (T074-T076)
- Message contracts can run in parallel within each category (commands: T045-T049, events: T050-T055)
- Once Foundational phase completes:
  - US1 can start immediately
  - US2 can start in parallel with US1 (different services)
  - US4 and US5 can start after US1-US3 in parallel (different concerns)
  - US6 can start after US2 in parallel with other stories (read-only)

---

## Parallel Example: Foundational Phase

```bash
# Launch all enum creation tasks together:
Task T018: "Create ProtocolType enum"
Task T019: "Create ExecutionStatus enum"
Task T020: "Create DiscoveryStatus enum"
Task T021: "Create AuthType enum"
Task T022: "Create AzureAuthType enum"

# Launch all protocol settings value objects together:
Task T024: "Create FtpProtocolSettings value object"
Task T025: "Create HttpsProtocolSettings value object"
Task T026: "Create AzureBlobProtocolSettings value object"

# Launch all repository implementations together:
Task T036: "Implement FileRetrievalConfigurationRepository"
Task T037: "Implement FileRetrievalExecutionRepository"
Task T038: "Implement DiscoveredFileRepository"

# Launch all protocol adapters together (User Story 2):
Task T074: "Implement FtpProtocolAdapter"
Task T075: "Implement HttpsProtocolAdapter"
Task T076: "Implement AzureBlobProtocolAdapter"
```

---

## Implementation Strategy

### MVP First (User Stories 1-3 Only)

1. Complete Phase 1: Setup (T001-T017)
2. Complete Phase 2: Foundational (T018-T059) - **CRITICAL - blocks all stories**
3. Complete Phase 3: User Story 1 (T060-T073) - Configuration CRUD
4. Complete Phase 4: User Story 2 (T074-T085) - Scheduled file checks
5. Complete Phase 5: User Story 3 (T086-T095) - Workflow integration
6. **STOP and VALIDATE**: Test end-to-end flow:
   - Create configuration via API
   - Verify scheduled check executes
   - Verify file discovered event published
   - Verify workflow command sent
7. Deploy/demo if ready (MVP complete!)

**MVP Delivers**:
- ✅ Configuration management via API (US1)
- ✅ Automated scheduled file checks (US2)
- ✅ Workflow orchestration integration (US3)
- ✅ Security trimming and multi-tenancy (US1)
- ✅ Idempotency and duplicate prevention (US3)
- ✅ All three protocols supported (FTP, HTTPS, Azure Blob)

### Incremental Delivery Beyond MVP

1. Add User Story 4 (T096-T105) → Test with 20+ configurations → Deploy (multi-configuration support)
2. Add User Story 5 (T106-T118) → Test update/delete lifecycle → Deploy (full configuration lifecycle)
3. Add User Story 6 (T119-T132) → Test execution monitoring → Deploy (operational observability)
4. Complete Phase 9: Polish (T133-T148) → Production hardening

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together (blocking work)
2. Once Foundational is done:
   - Developer A: User Story 1 (Configuration CRUD)
   - Developer B: User Story 2 (Scheduled checks + protocol adapters)
   - Developer C: Message contracts and NServiceBus setup (supports both A and B)
3. After US1 and US2 complete:
   - Developer A: User Story 5 (Update/Delete - extends US1)
   - Developer B: User Story 3 (Workflow integration - extends US2)
   - Developer C: User Story 4 (Multi-config support - enhances US1-US3)
4. Finally:
   - Developer A: User Story 6 (Monitoring - read-only observability)
   - Developer B: Polish tasks (performance, security hardening)
   - Developer C: Documentation and deployment automation

---

## Notes

- [P] tasks = different files, no dependencies within same phase
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Tests are not included in this task list (not requested in specification)
- Focus on implementation, structured logging, and constitution compliance
- All message handlers must be idempotent (SC-007: zero duplicate workflow triggers)
- All operations must respect client-scoped security trimming (SC-009: 100% enforcement)
- Date token replacement must be 100% accurate (SC-008)
- Scheduled checks must execute within 1 minute of scheduled time 99% of the time (SC-002)

---

## Summary Statistics

- **Total Tasks**: 148
- **Setup Phase**: 17 tasks
- **Foundational Phase**: 42 tasks (BLOCKING - must complete before user stories)
- **User Story 1** (P1 - MVP): 14 tasks (Configuration CRUD)
- **User Story 2** (P1 - MVP): 12 tasks (Scheduled file checks)
- **User Story 3** (P1 - MVP): 10 tasks (Workflow integration)
- **User Story 4** (P2): 10 tasks (Multi-configuration support)
- **User Story 5** (P2): 13 tasks (Update/Delete lifecycle)
- **User Story 6** (P3): 14 tasks (Execution monitoring)
- **Polish Phase**: 16 tasks (Cross-cutting concerns)

**MVP Scope (US1-US3)**: 59 tasks (Setup + Foundational + US1 + US2 + US3)

**Parallel Opportunities**:
- Setup: 12 tasks can run in parallel (different projects/configs)
- Foundational: 28 tasks can run in parallel (enums, value objects, repositories, contracts)
- User Stories: US1 and US2 can start in parallel after Foundational; US4, US5, US6 can run in parallel after their dependencies

**Independent Test Points**: Each user story (US1-US6) has a defined independent test that validates the story works in isolation.
