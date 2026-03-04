# Research: Policy Lifecycle Management Workflow

## Decision 1: Milestone model uses percentages of total ticks
- **Decision**: Lifecycle milestones are configured as percentages of total term ticks; milestone timestamps are computed from term start and tick duration.
- **Rationale**: Percentage semantics keep workflow behavior stable across different term lengths (6/10/24 ticks) and support accelerated test timelines.
- **Alternatives considered**:
  - Fixed milestone tick numbers (rejected: couples workflow behavior to one term length)
  - Manual absolute dates only (rejected: less reusable and harder to reason about in tests)

## Decision 2: Use staged cancellation for billing equity breaches
- **Decision**: If Billing reports equity at or below threshold, move to `PendingCancellation`, then cancel only if still below threshold after grace-window recheck.
- **Rationale**: Reduces false cancellations from temporary payment timing issues while preserving automated risk controls.
- **Alternatives considered**:
  - Immediate cancellation on first breach (rejected: too aggressive)
  - Require multiple consecutive breaches only (rejected: less direct control than grace-window model)

## Decision 3: Initial defaults for cancellation policy
- **Decision**: Use default threshold `-20%` equity and grace window `10%` of total ticks.
- **Rationale**: Matches stakeholder-provided domain example and supports configurable operation per product/state later.
- **Alternatives considered**:
  - Positive threshold values (rejected for initial model mismatch)
  - Fixed grace duration in absolute time (rejected in favor of tick-relative behavior)

## Decision 4: Correlation and workflow identity
- **Decision**: Correlate saga by `PolicyTermId`; include `PolicyId` and `PolicyTermId` in all lifecycle messages.
- **Rationale**: Allows overlapping term workflows while preserving single-saga-instance routing for each term.
- **Alternatives considered**:
  - Correlate by `PolicyId` only (rejected: cannot support overlapping terms cleanly)
  - Correlate by generated saga ID (rejected: weak domain traceability)

## Decision 5: End-of-term semantics
- **Decision**: Current term workflow ends in terminal `Renewed`, `Expired`, or `Cancelled`; accepted renewal emits next-term start event.
- **Rationale**: Enforces finite-term workflow boundaries and explicit handoff to new term process.
- **Alternatives considered**:
  - Continuous single workflow across terms (rejected: unclear lifecycle boundaries)

## Decision 6: Persistence strategy
- **Decision**: Cosmos DB with `/policyId` partition key and term state keyed by `PolicyTermId`.
- **Rationale**: Aligns with existing Policy service architecture and repository model; keeps term progression and policy projection writes partition-local.
- **Alternatives considered**:
  - Introduce PostgreSQL for workflow state (rejected due to stack divergence)
