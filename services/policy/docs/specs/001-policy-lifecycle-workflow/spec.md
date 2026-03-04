# Feature Specification: Policy Lifecycle Management Workflow (Quick)

**Feature Branch**: `existing branch (no new branch created)`  
**Created**: 2026-03-03  
**Status**: Draft  
**Input**: User description: "Policy lifecycle management workflow as a saga/state machine with tick-based time acceleration and renewal handoff"

> **Quick Spec Template**: Use this when domain docs already exist. Focus on scenarios, messages, and acceptance criteria only. Reference existing docs for patterns/standards.

---

## Context (References)

**Bounded Context**: Policy  
**Domain Docs**: `services/policy/docs/business/policy-management.md`, `services/policy/docs/overview.md`, `services/policy/docs/technical/policy-technical-spec.md`  
**Applies Rules**: Existing lifecycle statuses and transition rules (Bound → Issued → Active → Cancelled/Lapsed/Expired), cancellation constraints, and renewal process intent.

**Constitution**: `.specify/memory/constitution.md`  
**Project Structure**: `copilot-instructions/project-structure.md`  
**Messaging Patterns**: `copilot-instructions/messaging-patterns.md`

**Executable Host Profiles (required when applicable)**:
- **Api host present?** Yes → `Serilog.AspNetCore` + `mcr.microsoft.com/dotnet/aspnet:10.0`
- **Endpoint.In host present?** Yes → `Serilog.Extensions.Hosting` + `Serilog.Settings.Configuration` + `mcr.microsoft.com/dotnet/runtime:10.0`

---

## Clarifications

### Session 2026-03-03

- Q: Should renewal milestones be one, two, or three steps before term end? → A: Use percentage-based milestones of total ticks (not fixed tick numbers), and include billing-origin equity events that can drive cancellation decisions.
- Q: Should policy cancel immediately at equity threshold breach, or use a staged path? → A: Use staged path: enter `PendingCancellation` at threshold breach, then cancel only if still below threshold after a grace window measured as % of term ticks.

---

## What's New (The Delta)

This feature adds a **long-running saga workflow** that manages one policy term from issuance to term completion using configurable time ticks. The workflow emits lifecycle events and starts a separate workflow for the next term when renewal is accepted.

### Primary Scenario

**As a** Policy domain workflow orchestrator  
**I want to** run a policy term lifecycle as a message-driven state machine  
**So that** lifecycle transitions (active, pending cancellation, cancelled, pending renewal, expired/renewed) are deterministic, observable, and testable without waiting real months.

**Acceptance Criteria**:
1. **Given** a policy term starts with configurable total ticks and computed milestone dates, **When** the issuance/start event is handled, **Then** a `PolicyLifecycleSaga` instance is created and tracks that term using policy-term correlation.
2. **Given** a policy reaches configured lifecycle milestone percentages (for example renewal-open at 66% of elapsed ticks), **When** the workflow processes that milestone event, **Then** the policy remains `Active` and is additionally flagged `PendingRenewal`.
3. **Given** renewal is accepted before term end, **When** the acceptance event is handled, **Then** the current term workflow transitions to terminal `Renewed`, emits `PolicyTermCompleted`, and emits an event to start a new term workflow.
4. **Given** no renewal acceptance by term end, **When** term-end milestone is reached, **Then** the current term workflow transitions to terminal `Expired` and emits `PolicyTermCompleted` with completion reason `Expired`.
5. **Given** cancellation is requested during active term, **When** cancellation effective date arrives, **Then** the workflow transitions through `PendingCancellation` to `Cancelled` and completes that term workflow.
6. **Given** Billing publishes policy equity information that breaches configured cancellation threshold, **When** the workflow handles the billing event, **Then** the policy workflow enters `PendingCancellation` and starts a grace-window recheck.
7. **Given** a policy is in `PendingCancellation` due to low equity, **When** grace-window recheck occurs and equity remains below threshold, **Then** the workflow transitions to `Cancelled` and completes the term.

---

### Additional Scenarios (if any)

**Scenario 2**: Overlapping terms are supported
- **Given** term N is still in progress, **When** term N+1 is accepted and created, **Then** a new saga instance starts for term N+1 while term N continues until its own terminal state.

**Scenario 3**: Tick acceleration for non-production environments
- **Given** tick duration is configured in minutes, **When** policy term is created, **Then** all lifecycle milestone timestamps are computed from absolute dates and executed in accelerated time.

**Scenario 4**: Billing-driven cancellation recommendation
- **Given** Billing emits an equity-level event for a policy term, **When** equity falls below configured threshold, **Then** policy workflow records the recommendation, enters `PendingCancellation`, and applies a grace-window recheck before final cancellation.

---

### Edge Cases & Failure Modes

- What if duplicate lifecycle events arrive? → Saga and handlers must be idempotent and ignore already-applied transitions.
- What if milestone events arrive out of order? → Invalid transition events are ignored and logged with correlation context.
- What if cancellation and renewal acceptance happen near term-end? → Predefined transition precedence rules apply (cancellation effective immediately ends term; accepted renewal starts next term but does not reopen completed term).
- What if tick configuration is invalid (zero/negative duration, term ticks < 1)? → Term creation is rejected with validation failure event/response.
- What if Billing sends stale or duplicate equity events? → Workflow applies idempotency and ignores events older than latest processed equity timestamp for the term.
- What if equity recovers during grace window? → Workflow exits `PendingCancellation` and remains `Active` (with flags unchanged except cancellation recommendation cleared).

---

## Message Contracts (New/Changed)

### Commands (if any)

```csharp
// File: services/policy/src/Domain/Contracts/Commands/StartPolicyLifecycle.cs
public record StartPolicyLifecycle(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string IdempotencyKey,
    string PolicyId,
    string PolicyTermId,
    int TermTicks,
    DateTimeOffset EffectiveDate,
    DateTimeOffset ExpirationDate
);
```

```csharp
// File: services/policy/src/Domain/Contracts/Commands/ApplyPolicyLifecycleMilestone.cs
public record ApplyPolicyLifecycleMilestone(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string IdempotencyKey,
    string PolicyId,
    string PolicyTermId,
    string MilestoneType,
    DateTimeOffset MilestoneUtc
);
```

```csharp
// File: services/policy/src/Domain/Contracts/Commands/ProcessPolicyEquityUpdate.cs
public record ProcessPolicyEquityUpdate(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string IdempotencyKey,
    string PolicyId,
    string PolicyTermId,
    decimal EquityPercentage,
    decimal CancellationThresholdPercentage
);
```

**Purpose**: Start and advance policy-term workflow milestones.  
**Handler**: Endpoint.In handlers/saga orchestration in policy service.  
**Idempotency**: Deduplicate by `(PolicyTermId, MilestoneType)` and/or `IdempotencyKey`.

---

### Events (if any)

```csharp
// File: services/policy/src/Domain/Contracts/Events/PolicyRenewalWindowOpened.cs
public record PolicyRenewalWindowOpened(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string IdempotencyKey,
    string PolicyId,
    string PolicyTermId,
    DateTimeOffset RenewalWindowStartUtc
);
```

```csharp
// File: services/policy/src/Domain/Contracts/Events/PolicyTermCompleted.cs
public record PolicyTermCompleted(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string IdempotencyKey,
    string PolicyId,
    string PolicyTermId,
    string CompletionStatus,
    DateTimeOffset CompletedUtc
);
```

```csharp
// File: services/policy/src/Domain/Contracts/Events/PolicyRenewalAccepted.cs
public record PolicyRenewalAccepted(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string IdempotencyKey,
    string PolicyId,
    string CurrentPolicyTermId,
    string NextPolicyTermId,
    DateTimeOffset NextTermEffectiveDate,
    DateTimeOffset NextTermExpirationDate
);
```

```csharp
// File: services/policy/src/Domain/Contracts/Events/PolicyEquityLevelReported.cs
public record PolicyEquityLevelReported(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string IdempotencyKey,
    string PolicyId,
    string PolicyTermId,
    decimal EquityPercentage,
    decimal CancellationThresholdPercentage,
    bool CancellationRecommended
);
```

**Purpose**: Publish lifecycle progression and completion facts; trigger next-term workflow.  
**Subscribers**: Billing, Customer Communications, Reporting, downstream policy workflows.  
**Location**: Internal first; promote to `platform/RiskInsure.PublicContracts/Events/` if consumed cross-service.

---

## Workflow/Saga Design (if long-running)

- **Workflow Type**: Saga
- **Saga Name**: `PolicyLifecycleSaga`
- **Start Message**: `StartPolicyLifecycle`
- **Advancing Messages**: `ApplyPolicyLifecycleMilestone`, `PolicyRenewalAccepted`, `PolicyCancellationRequested`, `PolicyCancellationEffective`, `PolicyEquityLevelReported`
- **Correlation ID Field**: `PolicyTermId` (required on all saga-driving messages)
- **Correlation Mapping**: `mapper.MapSaga(data => data.PolicyTermId)` mapped to all start/advance message types
- **Saga State Fields**:
  - `PolicyId`
  - `PolicyTermId`
  - `CurrentStatus`
  - `StatusFlags` (includes `PendingRenewal`)
  - `EffectiveDate`, `ExpirationDate`
  - `RenewalWindowOpenDate`
  - `CancellationRequestedDate`, `CancellationEffectiveDate`
    - `CurrentEquityPercentage`, `CancellationThresholdPercentage`
    - `PendingCancellationStartedUtc`, `GraceWindowRecheckUtc`
    - `MilestonePercentages` (for example renewal-open, reminder, term-end)
  - `CompletionStatus`, `CompletedUtc`

**Constraints**:
- Saga acts as orchestrator only (message in → state update → message out)
- Saga performs no direct database/file/external service I/O
- Saga completion condition is explicit (`MarkAsComplete`) on `Cancelled`, `Expired`, or `Renewed`

---

## Data Changes (if applicable)

**Persistence**: Cosmos DB

### If Cosmos DB:
- **Partition Key**: `/policyId`
- **Document Types**: Existing `Policy`; new lifecycle state projection fields and/or a term-state document per `PolicyTermId`
- **State Transitions**:
  - `Bound` → `Issued` → `Active`
  - `Active (+ PendingRenewal flag)` while renewal is in progress
    - `Active` → `PendingCancellation` → `Cancelled` (if still below equity threshold at grace recheck)
  - `Active` → `Expired` (term end without acceptance)
  - `Active`/`PendingRenewal` → `Renewed` (accepted renewal for this term)

### Time Milestone Strategy:
- Store lifecycle milestone configuration as percentages of `TotalTicks`.
- Compute milestone trigger ticks and absolute timestamps at term start.
- Example: renewal-open at 66% of ticks means milestone tick = `ceil(TotalTicks * 0.66)`.

### Idempotency Strategy:
Use `IdempotencyKey` and `(PolicyTermId + lifecycle transition)` uniqueness rules so duplicate milestone/renewal/cancellation messages do not create duplicate state transitions or events.

---

## Non-Goals (Out of Scope)

- ❌ Mid-term endorsement workflow implementation
- ❌ Regulatory-state specific cancellation notice matrix
- ❌ Complex premium repricing/rerating algorithms for renewal offers

---

## Success Criteria (Testable)

- [ ] **SC-001**: In non-production tick mode, terms of varying lengths (for example 6, 10, 24 ticks) run end-to-end and reach a terminal status (`Cancelled`, `Expired`, or `Renewed`) using percentage-based milestone semantics.
- [ ] **SC-002**: 100% of lifecycle transition events include `PolicyId`, `PolicyTermId`, `MessageId`, `OccurredUtc`, and `IdempotencyKey`.
- [ ] **SC-003**: Duplicate delivery of any lifecycle message does not create extra transitions or duplicate completion events.
- [ ] **SC-004**: Renewal acceptance for term N always emits a start event for term N+1, and both term workflows can coexist during overlap.

---

## Definition of Done

- [ ] All acceptance criteria have passing tests (xUnit unit + Playwright integration)
- [ ] Message handlers and saga include correlation IDs in all log statements
- [ ] Idempotency verified (duplicate message replay tests pass)
- [ ] Domain test coverage ≥90%, handler/saga coverage ≥80%
- [ ] PR approved and merged

---

## Notes / Open Questions

- Assumption: Current-term terminal statuses are `Cancelled`, `Expired`, and `Renewed`; next term is always a new workflow instance.
- Assumption: Tick acceleration is implemented through computed absolute milestone dates from term start, configured tick duration, and configured milestone percentages.
- Assumption: `Bound` remains the pre-active status for a future term, per stakeholder decision.

**Dependencies**:
- Existing policy creation flow (`QuoteAccepted` → policy created) remains start trigger source.
- Renewal acceptance event source (likely Billing/Customer workflow) must publish with `PolicyTermId`.
