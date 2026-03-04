<!--
Sync Impact Report - Version 2.1.0 (2026-03-03)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Version Change: 2.0.0 → 2.1.0

Modified Principles:
- II. Data Model Strategy → II. Data Model Strategy (clarified persistence boundaries)
- VI. Message-Based Integration → VI. Message-Based Integration (metadata + topic declaration clarity)
- VII. Thin Message Handlers → VII. Thin Message Handlers (strict responsibility boundaries)
- IX. Technology Constraints → XI. Technology Constraints (renumbered after additions)

Added Principles:
- IX. Saga Workflow Orchestration

Removed Principles:
- None

Templates Requiring Updates:
- ✅ .specify/templates/plan-template.md
- ✅ .specify/templates/spec-template.md
- ✅ .specify/templates/tasks-template.md
- ✅ .specify/templates/README.md
- ✅ .specify/templates/constitution-template.md

Runtime/Guidance Docs:
- ✅ README.md

Command/Agent Guidance Files:
- ✅ .github/agents/speckit.constitution.agent.md (validated; no outdated agent-name references)
- ✅ .github/prompts/domain-builder.prompt.md

Follow-up TODOs:
- None
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
-->

# RiskInsure Architecture Constitution

**Version**: 2.1.0 | **Ratified**: 2026-02-02 | **Last Amended**: 2026-03-03

## Purpose

This constitution defines non-negotiable architectural rules for all projects within the
RiskInsure repository. All code, designs, and decisions MUST align with these principles.

Domain-specific standards MAY extend these rules for a bounded context, but they MUST NOT
contradict this constitution.

---

## Core Principles

### I. Domain Language Consistency

**The Rule**: Each bounded context MUST define and consistently use its own ubiquitous language.

**Requirements**:
- Domain terminology MUST be documented in domain-specific standards files.
- Code, contracts, APIs, and documentation MUST use approved domain terms.
- Ambiguous or conflicting terms MUST be rejected during review.

**Rationale**: Shared language keeps business intent explicit and reduces translation errors.

**Verification**: PR review checks naming in contracts, handlers, managers, and docs.

---

### II. Data Model Strategy

**The Rule**: Each feature MUST explicitly choose and document its persistence strategy.

**Requirements**:
- If using Cosmos DB, use a partition strategy aligned to the processing unit.
- If using PostgreSQL, use normalized schemas and explicit transactional boundaries.
- Persistence choice and rationale MUST be documented in `spec.md` and `plan.md` before coding.

**Rationale**: Explicit data strategy prevents accidental coupling and preserves consistency intent.

**Verification**: Feature artifacts and implementation are checked for strategy alignment.

---

### III. Atomic State Transitions

**The Rule**: Aggregate state and derived counters MUST transition atomically.

**Requirements**:
- Use optimistic concurrency for conflicting updates.
- Retry conflicts safely.
- Keep state transition and aggregate updates in one atomic boundary.

**Rationale**: Atomic transitions prevent drift and race-condition corruption.

**Verification**: Tests and code review confirm atomic updates and conflict handling.

---

### IV. Idempotent Message Handling

**The Rule**: All message handlers MUST be safe for retry and replay.

**Requirements**:
- Check existing state before create/update operations.
- Use deterministic identifiers from message data where possible.
- Duplicate deliveries MUST NOT create duplicate side effects.

**Rationale**: At-least-once delivery is expected in distributed systems.

**Verification**: Retry/replay tests and handler-level idempotency checks are required.

---

### V. Structured Observability

**The Rule**: Logs and telemetry MUST include correlation context required for traceability.

**Requirements**:
- Include processing-unit identifiers and entity identifiers when applicable.
- Include message correlation identifiers from NServiceBus context.
- Use structured logging fields, not concatenated strings.

**Rationale**: Correlated telemetry is mandatory for production diagnosis in async workflows.

**Verification**: PR review validates required fields in logging statements.

---

### VI. Message-Based Integration

**The Rule**: Cross-component integration MUST use brokered messaging.

**Requirements**:
- Commands MUST use `Send`; events MUST use `Publish`.
- Message contracts MUST include `MessageId`, `OccurredUtc`, and `IdempotencyKey`.
- Event publication MUST map to declared Service Bus topic infrastructure.
- Direct cross-service synchronous HTTP for business workflow integration is prohibited.

**Rationale**: Brokered integration preserves loose coupling and resilience.

**Verification**: Integration paths and published event topics are checked in review.

---

### VII. Thin Message Handlers

**The Rule**: Handlers MUST coordinate flow, not contain domain logic.

**Requirements**:
- Validate message shape quickly and fail fast.
- Delegate business behavior to domain managers/services.
- Publish or send resulting messages.
- Direct database access from handlers is prohibited.

**Rationale**: Thin handlers are simpler, more testable, and easier to evolve.

**Verification**: Handler classes are reviewed for delegation-only behavior.

---

### VIII. Repository Pattern

**The Rule**: Domain-owned repository abstractions MUST isolate persistence concerns.

**Requirements**:
- Repository interfaces live in Domain; implementations live in Infrastructure.
- Mapping between domain models and storage models occurs in Infrastructure.
- Domain and handler logic MUST use repository abstractions, not storage SDKs directly.

**Rationale**: Persistence isolation preserves domain purity and testability.

**Verification**: Layer dependency checks and project references enforce this rule.

---

### IX. Saga Workflow Orchestration

**The Rule**: Long-running workflows MUST be orchestrated by NServiceBus sagas that are message-driven
state machines and never direct integration workers.

**Requirements**:
- A saga MUST define explicit correlation mapping in `ConfigureHowToFindSaga(...)` before use.
- Correlation identifiers (for example `PolicyId`, `WorkflowId`, `OrderId`) MUST be present in every
  message that starts or advances the saga.
- Saga logic MUST be limited to: receive message, evaluate saga state, update saga data,
  and emit follow-up messages/events.
- Sagas MUST NOT call external services, files, or databases directly.
- Saga persistence data MUST contain only workflow state needed for routing/progress decisions.
- Saga completion MUST be explicit via `MarkAsComplete()` when terminal conditions are reached.

**Rationale**: A saga is a workflow coordinator, not a worker. Keeping orchestration pure avoids tight
coupling, improves recoverability, and makes retries deterministic.

**Verification**: Code review checks saga classes for correlation mapping, allowed responsibilities,
and absence of direct I/O dependencies.

---

### X. Test Coverage Requirements

**The Rule**: Minimum test coverage thresholds MUST be met for merge.

**Requirements**:
- Domain layer coverage target: 90%+.
- Application/handler coverage target: 80%+.
- Integration tests MUST cover key message and API flows.
- Saga tests MUST cover start, progression, timeout/terminal path, and duplicate message handling.

**Rationale**: Coverage gates reduce regressions in asynchronous, stateful workflows.

**Verification**: CI checks and test reports gate pull requests.

---

### XI. Technology Constraints

**The Rule**: Implementations MUST use the approved platform stack.

**Approved Stack**:
- .NET 10.0, C# latest with nullable reference types
- NServiceBus 9.x with RabbitMQ and/or Azure Service Bus transport
- Cosmos DB and/or PostgreSQL (feature decision required)
- Azure Container Apps for endpoint hosting
- xUnit for .NET tests and Playwright for API integration tests

**Prohibited**:
- Entity Framework Core for domain persistence patterns in this repository
- Distributed transactions across services
- Kafka as primary messaging transport

**Rationale**: Standardization lowers operational risk and design fragmentation.

**Verification**: Project/package review enforces approved technology use.

---

### XII. Naming Conventions

**The Rule**: Naming MUST communicate intent and message semantics clearly.

**Requirements**:
- Commands use imperative form (`VerbNoun`).
- Events use past-tense factual form (`NounVerbPastTense`).
- Handlers follow `{MessageName}Handler`.
- Saga data and correlation properties MUST use explicit domain names (no ambiguous abbreviations).

**Rationale**: Intent-revealing names improve maintainability and review accuracy.

**Verification**: Naming is enforced through code review and static checks where available.

---

## Development Workflow

### Feature Development Process

1. **Design**:
   - Identify impacted constitutional principles.
   - Define commands/events and workflow progression first.
   - If workflow is long-running, define saga boundaries and correlation fields.
2. **Test**:
   - Write failing tests before implementation when feasible.
   - Include idempotency and saga progression tests for workflow features.
3. **Implement**:
   - Implement contracts, managers/services, handlers, and saga orchestration.
   - Keep handlers and sagas free from direct external I/O integration logic.
4. **Verify**:
   - Run unit/integration tests.
   - Validate constitution compliance before merge.

### Code Review Requirements

Every PR MUST verify:
- Compliance with Principles I–XII
- Idempotent handlers and deterministic saga progression
- Required correlation fields and structured logs
- Coverage and quality gates

Reviewers MUST reject PRs that violate core principles.

---

## Governance

### Constitutional Authority

This constitution supersedes other project guidance when conflicts arise.
Conflicting guidance MUST be updated to align with this document.

### Amendment Process

1. Propose amendment with rationale and impact.
2. Review with maintainers/architects.
3. Approve and apply semantic version bump:
   - **MAJOR**: Breaking principle removal/redefinition
   - **MINOR**: New principle or materially expanded guidance
   - **PATCH**: Clarifications with no semantic change
4. Update dependent templates and guidance docs.
5. Communicate amendment to the team.

### Compliance Review Cadence

- Constitution compliance MUST be checked in every feature plan and PR.
- A lightweight constitutional review SHOULD occur at least once per quarter.

### Version History

- **2.1.0** (2026-03-03): Added Saga Workflow Orchestration principle and synchronized Spec Kit templates.
- **2.0.0** (2026-02-02): Simplified architecture constitution for cross-domain applicability.
- **1.0.0** (2026-01-03): Initial constitution.

---

## Related Documents

- [README.md](../../README.md)
- [copilot-instructions/project-structure.md](../../copilot-instructions/project-structure.md)
- [copilot-instructions/messaging-patterns.md](../../copilot-instructions/messaging-patterns.md)
- [copilot-instructions/testing-standards.md](../../copilot-instructions/testing-standards.md)
- [.specify/templates/plan-template.md](../templates/plan-template.md)

---

**End of Constitution**
