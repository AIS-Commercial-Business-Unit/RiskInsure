<!--
Sync Impact Report - Version 1.0.0 (2026-01-03)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Version Change: Initial → 1.0.0

Constitution Status:
  ✅ Created - Initial constitution derived from memory files
  ✅ 10 principles defined from domain patterns and standards
  ✅ Governance model established
  ✅ Compliance framework defined

Principles Defined:
  1. Bounded Context Isolation (NEW) - Domain boundaries, database per domain
  2. Message-Based Integration (NEW) - Event-driven architecture primary
  3. Eventual Consistency (NEW) - Async operations, no distributed transactions
  4. Test Coverage Requirements (NEW) - Domain 90%+, application 80%+
  5. API Async-First (NEW) - 202 Accepted pattern for commands
  6. Idempotent Operations (NEW) - Retry-safe handlers, duplicate handling
  7. Repository Pattern (NEW) - Domain defines, infrastructure implements
  8. Naming Consistency (NEW) - Commands (imperative), events (past tense)
  9.  Explicit Validation Layers (NEW) - API format validation, domain business rules

Templates Requiring Updates:
  ✅ .specify/memory/*.md - Source files used to derive principles
  ⚠ .specify/templates/plan-template.md - Verify alignment with principles
  ⚠ .specify/templates/spec-template.md - Ensure requirements align
  ⚠ .specify/templates/tasks-template.md - Task types match principles
  ⚠ .github/prompts/*.md - Update command prompts if needed

Follow-up Actions:
  □ Review template files for consistency with constitution
  □ Update command prompts to reference constitution principles
  □ Establish periodic constitution review cadence
  □ Train team on constitutional principles

Next Amendment: TBD (on principle violation or new pattern adoption)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# RiskInsure Architecture Constitution

**Version**: 2.0.0 | **Ratified**: 2026-02-02 | **Last Amended**: 2026-02-02

## Purpose

This constitution defines **non-negotiable architectural rules** for all projects within the RiskInsure repository. All code, designs, and decisions MUST align with these principles.

**Domain-Specific Standards**: See [docs/filerun-processing-standards.md](../docs/filerun-processing-standards.md) for ACH/NACHA file processing domain rules.

---

## Core Principles

### I. Domain Language Consistency

**The Rule**: Each domain must define and consistently use its own ubiquitous language across all code, documentation, and communication.

**Requirements**:
- Document domain terminology in domain-specific standards files (e.g., `docs/*-standards.md`)
- Use domain terms consistently in code, variable names, types, and messages
- Prohibit ambiguous or technical jargon when domain terms exist
- Maintain a glossary of required vs. prohibited terms per domain

**Rationale**: Consistent domain language prevents confusion, improves communication between business and technical teams, and aligns code with business concepts.

**Verification**: Code reviews must reject PRs using prohibited terms or inconsistent terminology.

**Verification**: No database connection strings reference other contexts; no cross-context entity references.

---

### II. Single-Partition Data Model

**The Rule**: For file processing systems, use a single Cosmos DB container partitioned by the processing run identifier.

**Requirements**:
- One container for all document types within a processing domain
- Partition key identifies the processing unit (e.g., `/fileRunId`, `/batchId`)
- Document type discriminator field distinguishes entity types
- All related documents co-located in same partition
- Queries within partition are free; cross-partition queries for reporting only

**Rationale**: Single-partition model enables free queries within a processing run, simplifies consistency, optimizes cost, and provides transactional guarantees.

**Verification**: All documents include partition key field; queries include partition key in WHERE clause.

---

### III. Atomic State Transitions

**The Rule**: Aggregate state and derived counts MUST be updated atomically when child entities transition states.

**Requirements**:
- Use optimistic concurrency (ETags) for aggregate updates
- Retry on ETag mismatch (conflicts)
- Update counts in same transaction as state change (same partition)
- Log state transitions with before/after counts
- Handle all transition scenarios including replays and retries

**Rationale**: Prevents lost updates in concurrent scenarios; maintains accurate aggregate state for reporting and completion detection.

**Verification**: All aggregate updates use ETags; count updates are atomic with state changes.

---

### IV. Idempotent Message Handlers

**The Rule**: All message handlers MUST be idempotent (safe to retry/replay).

**Requirements**:
- Check for existing state before creating documents
- Use message-provided identifiers (not auto-generated)
- Handle duplicate messages gracefully (no errors, just log)
- Support at-least-once delivery semantics
- Return early if operation already completed
- Use outbox pattern for exactly-once processing

**Rationale**: Message systems guarantee at-least-once delivery. Non-idempotent handlers create duplicate data and errors on retry.

**Verification**: All handlers check for existing state; retry tests demonstrate idempotency.

---

### V. Structured Observability

**The Rule**: All logs and telemetry MUST include relevant correlation identifiers.

**Requirements**:
- Include processing run identifier (e.g., `fileRunId`, `batchId`) in all logs
- Include entity identifier when operating on specific entities
- Include correlation ID from message context
- Include operation name for all logged operations
- Use structured logging (not string concatenation)

**Log Levels**:
- **Information**: State transitions, completion events, normal operations
- **Warning**: Retries, degraded conditions, non-critical issues
- **Error**: Failures requiring intervention, exceptions

**Rationale**: Correlation IDs enable distributed tracing across services and data operations. Essential for debugging production issues.

**Verification**: All log statements include required correlation fields.

---

### VI. Message-Based Integration

**The Rule**: Components integrate through brokered messages (commands and events) using NServiceBus with RabbitMQ transport.

**Message Types**:
- **Commands**: Imperative, unicast, directed actions (e.g., `ProcessEntity`)
- **Events**: Past-tense, broadcast, facts that occurred (e.g., `EntityProcessed`)

**Requirements**:
- Use `context.Send()` for commands (targeted endpoint)
- Use `context.Publish()` for events (subscribers)
- All messages include standard metadata (MessageId, OccurredUtc, correlation fields, IdempotencyKey)
- Define message contracts as C# records (immutable)

**Rationale**: Message-based integration maintains loose coupling, enables independent scaling, provides natural retry/error handling.

**Verification**: No direct service-to-service HTTP calls; all integration through message transport.

---

### VII. Thin Message Handlers

**The Rule**: Message handlers MUST be thin - validate input, delegate to services, publish events.

**Prohibited in Handlers**:
- ❌ Business logic implementation
- ❌ Direct data access (use repositories/services)
- ❌ Complex transformations
- ❌ Long-running operations

**Handler Responsibilities**:
1. Validate message structure (fast-fail)
2. Call domain service or repository
3. Publish resulting events
4. Log outcome

**Rationale**: Thin handlers are testable without message infrastructure, enable business logic reuse, and simplify maintenance.

**Verification**: Handlers delegate to services; no business logic in handler classes.

---

### VIII. Test Coverage Requirements

**The Rule**: Code MUST meet minimum test coverage thresholds before merging.

**Coverage Targets**:
- **Domain Layer**: 90%+ coverage (entities, aggregates, value objects)
- **Application Layer**: 80%+ coverage (services, handlers, commands)
- **Infrastructure Layer**: Integration tests for repositories and external services
- **API Layer**: Integration tests for endpoints

**Test Standards**:
- xUnit framework for all tests
- AAA pattern (Arrange-Act-Assert)
- One assertion focus per test
- Test naming: `MethodName_Scenario_ExpectedBehavior`
- Prefer fakes over mocks
- No tests against implementation details

**Rationale**: High test coverage ensures business logic correctness, enables confident refactoring, and catches regressions early.

**Verification**: Code coverage reports in CI; PR gates block merge below thresholds.

---

### IX. Technology Constraints

**The Rule**: Use approved technology stack; prohibited technologies are strictly forbidden.

**Approved Stack**:
- ✅ .NET 10.0
- ✅ C# 13 with nullable reference types enabled
- ✅ Azure Cosmos DB
- ✅ RabbitMQ for messaging transport
- ✅ NServiceBus 9.x+ with RabbitMQ transport
- ✅ Azure Logic Apps Standard for orchestration workflows
- ✅ Azure Container Apps for hosting NServiceBus endpoints
- ✅ xUnit for testing

**Prohibited Technologies**:
- ❌ Entity Framework Core (use repository pattern with Cosmos SDK)
- ❌ Distributed transactions (`TransactionScope`)
- ❌ Azure Functions (use Azure Container Apps)
- ❌ Kafka (not part of approved messaging stack)

**Rationale**: Standardized stack ensures consistency, leverages team expertise, and prevents anti-patterns.

**Verification**: Project references reviewed in code reviews; prohibited packages blocked.

---

### X. Naming Conventions

**The Rule**: Naming follows strict conventions that communicate intent.

**Message Naming**:
- Commands: `Verb` + `Noun` (e.g., `ProcessEntity`, `CreateRecord`)
- Events: `Noun` + `VerbPastTense` (e.g., `EntityProcessed`, `RecordCreated`)

**Code Naming**:
- Records/DTOs: `EntityName` (e.g., `FileReceived`, `PaymentReady`)
- Handlers: `MessageName` + `Handler` (e.g., `FileReceivedHandler`)
- Services: `FunctionalService` (e.g., `EntityProcessor`)
- Repositories: `I` + `EntityName` + `Repository` (e.g., `IFileRunRepository`)
- Avoid abbreviations except common domain terms

**Rationale**: Consistent naming improves readability and communicates intent. Commands sound like actions; events like history.

**Verification**: Code reviews enforce naming standards.

**Verification**: All handlers check for existing state; integration tests verify retry safety.

---

### VIII. Repository Pattern

**The Rule**: All data access goes through repository interfaces defined in Domain, implemented in Infrastructure.

**Requirements**:
- Domain defines `IEntityRepository` interfaces
- Infrastructure provides `EntityRepository` implementations
- Repositories work with domain entities, not DTOs or documents
- Mapping between entities and storage documents happens in Infrastructure
- Application layer depends on repository interfaces only
- Repositories return domain entities or collections

**Prohibited**:
- Direct `CosmosClient` usage outside repositories
- Entity Framework `DbContext` references in Domain or Application
- Data access code in handlers or services

**Rationale**: Repository pattern enables testability (mock repositories), maintainability (swap implementations), and clean separation between domain and persistence.

**Verification**: No data access code outside Infrastructure; handlers use interfaces only.

---

## Development Workflow

### Feature Development Process

1. **Design Phase**:
   - Identify affected principles from this constitution
   - Review domain-specific standards if applicable
   - Design messages (commands/events) first
   - Document in `docs/` if substantial change

2. **Test Phase**:
   - Write failing tests before implementation (TDD encouraged)
   - Ensure tests cover domain logic thoroughly

3. **Implementation Phase**:
   - Implement message contracts (records)
   - Add message handlers (thin, delegate to services)
   - Implement services/repositories
   - Update workflows if needed

4. **Verification Phase**:
   - Run all tests (unit + integration)
   - Verify code coverage meets thresholds
   - Review against constitutional principles
   - Ensure naming conventions followed

### Code Review Requirements

**Every PR MUST verify**:
- ✅ Principles I-X compliance
- ✅ Test coverage meets thresholds
- ✅ Naming conventions followed
- ✅ No prohibited technologies used
- ✅ All message handlers are idempotent
- ✅ Logs include required correlation fields

**Reviewers MUST reject PRs that**:
- Violate any core principle
- Fall below test coverage thresholds
- Use prohibited technologies
- Have non-idempotent handlers

**Hosting**:
- Docker containers (Linux-based)
- Azure Container Apps for deployment

**Rationale**: Standardized stack ensures consistency, leverages team expertise, enables code reuse, and simplifies operations.

### Prohibited Technologies

**The following are explicitly prohibited**:
- ❌ Entity Framework Core (use repository pattern with Cosmos SDK)
- ❌ Distributed transactions (`TransactionScope` across services)
- ❌ Synchronous HTTP calls between bounded contexts
- ❌ Shared databases between bounded contexts
- ❌ Kafka (not part of the approved messaging transport stack)

**Rationale**: These constraints prevent anti-patterns that conflict with core principles.

---

## Development Workflow

### Feature Development Process

1. **Design Phase**:
   - Document feature in `.specify/specs/` if substantial
   - Identify affected principles from this constitution
   - Review domain-specific standards if applicable
   - Design messages (commands/events) first
   - Document in `docs/` if substantial change

2. **Test Phase**:
   - Write failing tests before implementation (TDD encouraged)
   - Ensure tests cover domain logic thoroughly

3. **Implementation Phase**:
   - Implement message contracts (records)
   - Add message handlers (thin, delegate to services)
   - Implement services/repositories
   - Update workflows if needed

4. **Verification Phase**:
   - Run all tests (unit + integration)
   - Verify code coverage meets thresholds
   - Review against constitutional principles
   - Ensure naming conventions followed

### Code Review Requirements

**Every PR MUST verify**:
- ✅ Principles I-X compliance
- ✅ Test coverage meets thresholds
- ✅ Naming conventions followed
- ✅ No prohibited technologies used
- ✅ All message handlers are idempotent
- ✅ Logs include required correlation fields

**Reviewers MUST reject PRs that**:
- Violate any core principle
- Fall below test coverage thresholds
- Use prohibited technologies
- Have non-idempotent handlers

---

## Governance

### Constitutional Authority

This constitution **supersedes all other practices or guidelines** in this repository. When conflicts arise:

1. This constitution takes precedence
2. Update conflicting guidance to align
3. If principle is unworkable, amend constitution (see below)

### Amendment Process

**When to Amend**:
- Core principle proves unworkable in practice
- New architectural pattern adopted
- Technology constraint changes
- Principle conflicts discovered

**Amendment Procedure**:
1. **Propose Amendment**: Document issue, proposed change, rationale, impact
2. **Team Review**: Discuss with team and architect
3. **Approval**: Requires architect sign-off
4. **Version Bump**: 
   - **MAJOR**: Breaking principle change
   - **MINOR**: New principle added
   - **PATCH**: Clarification only
5. **Update Documents**: Sync related documentation
6. **Announce**: Communicate changes to team

### Version History

- **2.0.0** (2026-02-02): Simplified for RiskInsure; removed domain-specific details; made generic across all projects
- **1.0.0** (2026-01-03): Initial constitution

---

## Related Documents

**Domain-Specific Standards**:
- [docs/filerun-processing-standards.md](../docs/filerun-processing-standards.md) - ACH/NACHA file processing rules

**General Documentation**:
- [docs/architecture.md](../docs/architecture.md) - System architecture overview
- [docs/message-contracts.md](../docs/message-contracts.md) - Message contract specifications
- [.github/copilot-instructions.md](../.github/copilot-instructions.md) - Copilot coding assistant rules

---

**End of Constitution**
