# RiskInsure Constitution (Reference)

**Note**: RiskInsure already has an established constitution. This template file points to it.

**Primary Constitution**: [.specify/memory/constitution.md](../../.specify/memory/constitution.md)

## Core Principles Summary

RiskInsure follows these **non-negotiable** architectural principles:

### I. Domain Language Consistency
Each bounded context uses ubiquitous language from domain-specific standards. Prohibited terms are explicitly documented per domain.

### II. Single-Partition Data Model (Cosmos) OR Normalized Schema (PostgreSQL)
- **Cosmos**: One container per domain, partitioned by processing unit (e.g., `/fileRunId`, `/orderId`)
- **PostgreSQL**: Normalized schema with proper foreign keys and indexes

### III. Atomic State Transitions
Aggregate state and derived counts updated atomically using ETags (Cosmos) or transactions (PostgreSQL).

### IV. Idempotent Message Handlers
All handlers check existing state before creating, safe to retry/replay (at-least-once delivery).

### V. Structured Observability
All logs include correlation IDs: processing unit identifier + message identifier + operation name.

### VI. Message-Based Integration
Services integrate via Azure Service Bus (NServiceBus). No cross-service HTTP calls for business operations.

### VII. Thin Message Handlers
Handlers validate → delegate to domain services → publish events. No business logic in handlers.

### VIII. Test Coverage Requirements
- Domain layer: 90%+ coverage
- Application services/handlers: 80%+ coverage
- Integration tests for API endpoints (Playwright)

### IX. Technology Constraints
- .NET 10.0, C# 13 with nullable reference types
- NServiceBus 9.x with Azure Service Bus transport
- Azure Cosmos DB **OR** PostgreSQL (decision required per feature)
- xUnit for unit tests, Playwright for integration tests

## Additional Standards

**Project Structure**: [copilot-instructions/project-structure.md](../../copilot-instructions/project-structure.md)  
**Messaging Patterns**: [copilot-instructions/messaging-patterns.md](../../copilot-instructions/messaging-patterns.md)  
**Data Patterns**: [copilot-instructions/data-patterns.md](../../copilot-instructions/data-patterns.md)  
**API Conventions**: [copilot-instructions/api-conventions.md](../../copilot-instructions/api-conventions.md)  
**Testing Standards**: [copilot-instructions/testing-standards.md](../../copilot-instructions/testing-standards.md)

## Governance

All code, designs, and decisions MUST align with the constitution. Domain-specific standards (in `services/<domain>/docs/domain-specific-standards.md`) extend but cannot contradict core principles.

**Version**: 2.0.0 | **Ratified**: 2026-02-02 | **Last Amended**: 2026-02-02

---

**Usage in Spec Kit**: When running `/speckit.plan`, the agent will verify compliance with these principles via the "Constitution Check" section.
