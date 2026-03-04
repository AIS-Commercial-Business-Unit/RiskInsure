# Feature Specification: [FEATURE NAME] (Quick)

**Feature Branch**: `[###-feature-name]`  
**Created**: [DATE]  
**Status**: Draft  
**Input**: User description: "$ARGUMENTS"

> **Quick Spec Template**: Use this when domain docs already exist. Focus on scenarios, messages, and acceptance criteria only. Reference existing docs for patterns/standards.

---

## Context (References)

**Bounded Context**: [Service name - e.g., Billing, Policy, FileIntegration]  
**Domain Docs**: [Link: `services/<domain>/docs/domain-specific-standards.md`]  
**Applies Rules**: [Specific section/rule references from domain docs, or "General platform only"]

**Constitution**: [.specify/memory/constitution.md](../../.specify/memory/constitution.md)  
**Project Structure**: [copilot-instructions/project-structure.md](../../copilot-instructions/project-structure.md)  
**Messaging Patterns**: [copilot-instructions/messaging-patterns.md](../../copilot-instructions/messaging-patterns.md)

**Executable Host Profiles (required when applicable)**:
- **Api host present?** [Yes/No] → `Serilog.AspNetCore` + `mcr.microsoft.com/dotnet/aspnet:10.0`
- **Endpoint.In host present?** [Yes/No] → `Serilog.Extensions.Hosting` + `Serilog.Settings.Configuration` + `mcr.microsoft.com/dotnet/runtime:10.0`

---

## What's New (The Delta)

*Focus here: describe only what's changing/being added, not what already exists.*

### Primary Scenario

**As a** [user/system/role]  
**I want to** [action/goal]  
**So that** [business value]

**Acceptance Criteria**:
1. **Given** [context], **When** [action], **Then** [outcome]
2. **Given** [context], **When** [action], **Then** [outcome]
3. **Given** [error case], **When** [action], **Then** [failure outcome]

---

### Additional Scenarios (if any)

**Scenario 2**: [Brief title]
- **Given** [context], **When** [action], **Then** [outcome]

**Scenario 3**: [Brief title]
- **Given** [context], **When** [action], **Then** [outcome]

---

### Edge Cases & Failure Modes

- What if [concurrent operation]?
- What if [message arrives out of order]?
- What if [duplicate message replayed]?
- What if [external dependency fails]?

---

## Message Contracts (New/Changed)

### Commands (if any)

```csharp
// File: services/<domain>/src/Domain/Contracts/[CommandName].cs
public record [CommandName](
    Guid MessageId,              // Required
    DateTimeOffset OccurredUtc,  // Required
    string IdempotencyKey,       // Required
    [Type] [DomainField1],       // Your domain data
    [Type] [DomainField2]
);
```

**Purpose**: [What this command triggers]  
**Handler**: Will create in `services/<domain>/src/Infrastructure/Handlers/`  
**Idempotency**: [How duplicates handled - e.g., "Check existing by [field]"]

---

### Events (if any)

```csharp
// File: services/<domain>/src/Domain/Contracts/[EventName].cs (or PublicContracts if cross-service)
public record [EventNamePastTense](
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string IdempotencyKey,
    [Type] [DomainField1],
    [Type] [DomainField2]
);
```

**Purpose**: [What happened]  
**Subscribers**: [Which other services/handlers care about this]  
**Location**: [Internal or `platform/RiskInsure.PublicContracts/Events/`]

---

## Workflow/Saga Design (if long-running)

- **Workflow Type**: [Saga | Choreography-only | N/A]
- **Saga Name**: [e.g., `PolicyLifecycleSaga`]
- **Start Message**: [Message that creates saga instance]
- **Advancing Messages**: [Messages/events that move saga state]
- **Correlation ID Field**: [e.g., `PolicyId`, `WorkflowId`] (required on all saga-driving messages)
- **Correlation Mapping**: [How `ConfigureHowToFindSaga` maps fields]
- **Saga State Fields**: [Minimal state-only data required for progression]

**Constraints**:
- Saga acts as orchestrator only (message in → state update → message out)
- Saga performs no direct database/file/external service I/O
- Saga completion condition is explicit (`MarkAsComplete`)

---

## Data Changes (if applicable)

**Persistence**: [Cosmos DB | PostgreSQL - must choose one]

### If Cosmos DB:
- **Partition Key**: `/[fieldName]` (e.g., `/fileRunId`, `/orderId`)
- **Document Types**: [New types being added to container, e.g., "Invoice", "Payment"]
- **State Transitions**: [If applicable - e.g., "Invoice: Draft → Sent → Paid → Cancelled"]

### If PostgreSQL:
- **New Tables**: [Table names]
- **Schema Changes**: [Migrations needed]
- **Foreign Keys**: [Relationships]

### Idempotency Strategy:
[How handler prevents duplicates - e.g., "Check if document with MessageId exists before creating", "Upsert by OrderId+LineItemId"]

---

## Non-Goals (Out of Scope)

*Explicitly state what this feature does NOT include to prevent scope creep.*

- ❌ [Thing not included]
- ❌ [Future enhancement]
- ❌ [Related but separate concern]

---

## Success Criteria (Testable)

- [ ] **SC-001**: [Measurable outcome - e.g., "Message processed in <500ms p95"]
- [ ] **SC-002**: [User outcome - e.g., "User sees confirmation within 2 seconds"]
- [ ] **SC-003**: [System outcome - e.g., "Handles 1000 messages/min without errors"]

---

## Definition of Done

- [ ] All acceptance criteria have passing tests (xUnit unit + Playwright integration)
- [ ] Message handlers include correlation IDs in all log statements
- [ ] Idempotency verified (duplicate message replay test passes)
- [ ] Domain test coverage ≥90%, handler coverage ≥80%
- [ ] PR approved, merged to main

---

## Notes / Open Questions

[NEEDS CLARIFICATION] markers go here:
- [Question 1]?
- [Question 2]?

**Dependencies**: [Any blocked-by items or prerequisite features]
