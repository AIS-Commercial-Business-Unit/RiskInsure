# Feature Specification (EventStorming-Based)

**Status**: Draft  
**Created**: 2026-02-16  
**Domain Expert(s)**: John  
**Author**: [Developer]  
**EventStorming Model**: [services/.nsb_example/Sales_Systems_single_context_final.md](../../../../services/.nsb_example/Sales_Systems_single_context_final.md)

---

## Overview

**Feature Name**: Sales Ordering (Place Order)

**Bounded Context**: Sales

**Problem Statement**: Enable the Sales context to place an order on behalf of a client.

**Business Value**: Establishes the core ordering capability so downstream billing and shipping workflows can begin.

---

## Domain Model Reference

**EventStorming Model**: [services/.nsb_example/Sales_Systems_single_context_final.md](../../../../services/.nsb_example/Sales_Systems_single_context_final.md)

### Key Domain Elements (from EventStorming)

**Units of Work**:
- PlacingOrder
  - Type: Transaction Script
  - Invariants:
    - OrderId must be provided and non-empty
    - OrderId is unique within the Sales context (no duplicate orders)
    - Order must start in a "Placed" state on creation

**Commands**:
```csharp
namespace RiskInsure.Sales.Domain.Contracts;

public record PlaceOrder(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid OrderId,
    string IdempotencyKey
);
```

**Events** (Published):
```csharp
namespace RiskInsure.Sales.Domain.Contracts;

public record OrderPlaced(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid OrderId,
    string IdempotencyKey
);
```

**Policies** (Event Handlers):
- MustPlaceOrder: When Client requests order in Sales view -> Send PlaceOrder
  - Handler: OnPlaceOrder
  - External Subscription: None

**External Integrations**:
- **Subscribes to**: None
- **Publishes**: OrderPlaced (for downstream Billing/Shipping)

---

## RiskInsure Context

### Bounded Context
- **Service**: services/nsb.sales/
- **Domain Documentation**: (not yet created)
- **Domain-Specific Standards**: (none)

### Constitution Compliance
- [ ] Principle I: Domain language used consistently (matches EventStorming ubiquitous language)
- [ ] Principle II: Single-partition data model identified
- [ ] Principle III: Atomic state transitions planned
- [ ] Principle IV: Idempotent handlers designed (check existing state before creating)
- [ ] Principle V: Structured logging with correlation IDs
- [ ] Principle VI: Message-based integration (no direct HTTP between domains)
- [ ] Principle VII: Thin handlers (validate -> call manager -> publish)
- [ ] Principle VIII: Test coverage planned (Domain: 90%+, Handlers: 80%+)
- [ ] Principle IX: Technology constraints followed (.NET 10, NServiceBus 9.x)

### Message Contracts Metadata
All commands and events MUST include:
- MessageId (Guid)
- OccurredUtc (DateTimeOffset)
- IdempotencyKey (string)

Commands: Imperative names (PlaceOrder)  
Events: Past-tense names (OrderPlaced)

### Data Strategy

**Partition Key**: /orderId  
- [ ] Confirm partition key selection with domain experts

**Idempotency**:
- [x] Idempotency strategy defined
  - Use IdempotencyKey plus OrderId to detect duplicates
  - If an OrderDocument with the same OrderId already exists, ignore the command and do not publish OrderPlaced again

**Document Types**:
- OrderDocument (Order header)

---

## Implementation Refinements

> Note: The EventStorming model captures the domain logic. This section adds technical implementation details not covered in the EventStorming session.

### Data Type Refinements

From EventStorming Model -> Refined Types:
- OrderID: {GUID} -> Guid OrderId

### Business Rules & Invariants

**Unit of Work: PlacingOrder**:
1. Reject duplicate OrderId (idempotency)
2. Create a new order record with initial state "Placed"
3. Require OrderId on all transitions and logs

**Policy: MustPlaceOrder**:
- Trigger: Client initiates order via Sales view
- Action: Send PlaceOrder command with OrderId
- Error Handling: If duplicate OrderId detected, log and exit without publishing

### Persistence Technology Decision

REQUIRED: Choose one:
- [x] Azure Cosmos DB (NoSQL, single-partition, horizontal scaling)
  - Partition Key: /orderId
  - Rationale: Single-partition order flow, high write throughput, and event-driven integration align with Cosmos DB
- [ ] PostgreSQL (Relational, transactional, complex queries)
  - Schema: [Describe tables]
  - Rationale: [Why PostgreSQL for this feature?]

### Compensating Actions

From EventStorming "Compensatory Instructions":
- None specified

---

## Acceptance Criteria

### Scenario: MustPlaceOrder

Given a client initiates an order in the Sales view  
When the Sales context processes the request  
Then the PlaceOrder command is issued with OrderId  
And the OrderPlaced event is published

Verification:
- [ ] OrderPlaced published with OrderId
- [ ] Handler idempotent (reprocessing same message has no effect)
- [ ] Structured logs include correlation ID and OrderId

---

## Testing Strategy

### Unit Tests (Domain Layer)
- [ ] PlacingOrder logic tested independently
- [ ] Business rules validated
- [ ] Invariants enforced
- Coverage Target: 90%+

### Integration Tests (Handlers)
- [ ] MustPlaceOrder handler processes input correctly
- [ ] PlaceOrder command sent with required metadata
- [ ] OrderPlaced event published with required metadata
- [ ] Idempotency verified (duplicate message handling)
- Coverage Target: 80%+

### Integration Points
- [ ] Published events conform to public contracts

---

## Non-Goals

- UI implementation for Sales view
- Cross-context billing or shipping handling

---


## References

- **EventStorming Model**: [services/.nsb_example/Sales_Systems_single_context_final.md](../../../../services/.nsb_example/Sales_Systems_single_context_final.md)
- **Constitution**: [.specify/memory/constitution.md](../../../../.specify/memory/constitution.md)
- **Project Structure**: [copilot-instructions/project-structure.md](../../../../copilot-instructions/project-structure.md)
- **Public Contracts**: [platform/RiskInsure.PublicContracts/](../../../../platform/RiskInsure.PublicContracts/)
