# Feature Specification (EventStorming-Based)

**Status**: Draft | In Review | Approved  
**Created**: YYYY-MM-DD  
**Domain Expert(s)**: [Names from EventStorming session]  
**Author**: [Developer who created this spec]  
**EventStorming Model**: [Link to `_Systems_single_context_final.md` file]

---

## Overview

**Feature Name**: [Short descriptive name]

**Bounded Context**: [e.g., Sales, Billing, Shipping, Policy, Customer]

**Problem Statement**: [1-2 sentences: What business problem does this solve?]

**Business Value**: [Why are we building this now?]

---

## Domain Model Reference

**EventStorming Model**: [`services/{context}/.nsb_example/{Context}_Systems_single_context_final.md`](../path/to/eventstorming-model.md)

### Key Domain Elements (from EventStorming)

**Units of Work**:
- [List each Unit of Work from EventStorming model]
  - Type: [Transaction Script | Aggregate]
  - Invariants: [List from model]

**Commands**:
```csharp
// Example from model - refine parameter types
public record PlaceOrder(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid OrderId,
    string CustomerId,
    string IdempotencyKey
);
```

**Events** (Published):
```csharp
// Example from model - refine data elements
public record OrderPlaced(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid OrderId,
    string CustomerId,
    DateTimeOffset PlacedUtc,
    string IdempotencyKey
);
```

**Policies** (Event Handlers):
- [Policy name]: When `[Event]` → Send `[Command]`
  - Handler: [Handler name from model]
  - External Subscription: [Context].[Event]

**External Integrations**:
- **Subscribes to**: [List external events this context consumes]
- **Publishes**: [List events other contexts consume]

---

## RiskInsure Context

### Bounded Context
- **Service**: `services/[context-name]/`
- **Domain Documentation**: [Link to `services/{context}/docs/overview.md`]
- **Domain-Specific Standards**: [Link to `services/{context}/docs/domain-specific-standards.md` if exists]

### Constitution Compliance
- [ ] **Principle I**: Domain language used consistently (matches EventStorming ubiquitous language)
- [ ] **Principle II**: Single-partition data model identified
- [ ] **Principle III**: Atomic state transitions planned
- [ ] **Principle IV**: Idempotent handlers designed (check existing state before creating)
- [ ] **Principle V**: Structured logging with correlation IDs
- [ ] **Principle VI**: Message-based integration (no direct HTTP between domains)
- [ ] **Principle VII**: Thin handlers (validate → call manager → publish)
- [ ] **Principle VIII**: Test coverage planned (Domain: 90%+, Handlers: 80%+)
- [ ] **Principle IX**: Technology constraints followed (.NET 10, NServiceBus 9.x)

### Message Contracts Metadata
**All commands and events MUST include**:
- `MessageId` (Guid)
- `OccurredUtc` (DateTimeOffset)
- `IdempotencyKey` (string)

**Commands**: Imperative names (e.g., `PlaceOrder`, `BillOrder`)  
**Events**: Past-tense names (e.g., `OrderPlaced`, `OrderBilled`)

### Data Strategy

**Partition Key**: `/[fieldName]`  
- [ ] Identified from Unit of Work (e.g., `/orderId`, `/customerId`, `/policyId`)

**Idempotency**:
- [ ] How will duplicate messages be detected? (IdempotencyKey, existing entity check, etc.)

**Document Types** (in same container):
- [List entity types that will co-locate in this partition]

---

## Implementation Refinements

> **Note**: The EventStorming model captures the **domain logic**. This section adds **technical implementation details** not covered in the EventStorming session.

### Data Type Refinements

**From EventStorming Model** → **Refined Types**:
- `OrderID: {GUID}` → `Guid OrderId` (C# naming conventions)
- `[Add other parameter refinements here]`

### Business Rules & Invariants

**Unit of Work: [Name]**:
1. [Specific business rule from domain experts]
2. [Validation logic]
3. [Invariants that must hold]

**Policy: [Name]**:
- **Trigger**: When `[Event]` received
- **Condition**: [Any preconditions?]
- **Action**: Send `[Command]` with parameters `[list]`
- **Error Handling**: [What if command fails?]

### Persistence Technology Decision

**REQUIRED**: Choose one:
- [ ] **Azure Cosmos DB** (NoSQL, single-partition, horizontal scaling)
  - Partition Key: `/[field]`
  - Rationale: [Why Cosmos for this feature?]
- [ ] **PostgreSQL** (Relational, transactional, complex queries)
  - Schema: [Describe tables]
  - Rationale: [Why PostgreSQL for this feature?]

### Compensating Actions

**From EventStorming "Compensatory Instructions"**:
- [List compensating events or saga patterns if operations must be reversible]

---

## Acceptance Criteria

### Scenario: [Name from EventStorming Policy]

**Given**: [Preconditions]  
**When**: [Event received / Command sent]  
**Then**: [Expected outcome from EventStorming model]

**Verification**:
- [ ] Event `[Name]` published with correct data elements
- [ ] External subscribers receive event (if published externally)
- [ ] Handler idempotent (reprocessing same message has no effect)
- [ ] Structured logs include correlation ID and entity ID

---

## Testing Strategy

### Unit Tests (Domain Layer)
- [ ] Units of Work logic tested independently
- [ ] Business rules validated
- [ ] Invariants enforced
- **Coverage Target**: 90%+

### Integration Tests (Handlers)
- [ ] Policy handlers process events correctly
- [ ] Commands sent with correct parameters
- [ ] Events published with required metadata
- [ ] Idempotency verified (duplicate message handling)
- **Coverage Target**: 80%+

### Integration Points
- [ ] External event subscriptions tested (mock publishers)
- [ ] Published events conform to public contracts

---

## Non-Goals

- [What this feature explicitly does NOT do]
- [Deferred functionality for future iterations]

---

## Open Questions

- [ ] [Questions requiring domain expert clarification]
- [ ] [Technical unknowns to resolve before implementation]

---

## References

- **EventStorming Model**: [Link to `_Systems_single_context_final.md`]
- **Constitution**: [.specify/memory/constitution.md](../../.specify/memory/constitution.md)
- **Project Structure**: [copilot-instructions/project-structure.md](../../copilot-instructions/project-structure.md)
- **Domain Overview**: [services/{context}/docs/overview.md](../services/{context}/docs/overview.md)
- **Public Contracts**: [platform/RiskInsure.PublicContracts/](../../platform/RiskInsure.PublicContracts/)
