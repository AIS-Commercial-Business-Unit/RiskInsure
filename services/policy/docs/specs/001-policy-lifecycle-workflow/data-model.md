# Data Model: Policy Lifecycle Management Workflow

## Entity: PolicyLifecycleTermState
- **Purpose**: Saga/projection state for a single policy term workflow.
- **Identity**:
  - `policyTermId` (string, unique term workflow identity)
  - `policyId` (string, parent policy identity; partition key context)
- **Core fields**:
  - `currentStatus` (enum/string: Bound, Issued, Active, PendingCancellation, Cancelled, Expired, Renewed)
  - `statusFlags` (set: PendingRenewal)
  - `effectiveDateUtc` (DateTimeOffset)
  - `expirationDateUtc` (DateTimeOffset)
  - `termTicks` (int > 0)
  - `tickDuration` (TimeSpan/config reference)
  - `milestonePercentages` (object)
    - `renewalOpenPercent` (decimal)
    - `renewalReminderPercent` (decimal, optional)
    - `termEndPercent` (decimal = 100)
  - `computedMilestonesUtc` (object)
    - `renewalOpenUtc`
    - `renewalReminderUtc` (optional)
    - `termEndUtc`
  - `currentEquityPercentage` (decimal)
  - `cancellationThresholdPercentage` (decimal; default -20)
  - `pendingCancellationStartedUtc` (DateTimeOffset?)
  - `graceWindowPercent` (decimal; default 10)
  - `graceWindowRecheckUtc` (DateTimeOffset?)
  - `completionStatus` (Cancelled, Expired, Renewed, null)
  - `completedUtc` (DateTimeOffset?)

## Entity: PolicyLifecycleMilestone
- **Purpose**: Idempotency and audit of milestone processing.
- **Identity**:
  - Composite logical key: `policyTermId + milestoneType`
- **Core fields**:
  - `milestoneType` (RenewalOpen, RenewalReminder, TermEnd, GraceRecheck)
  - `occurredUtc`
  - `processedMessageId`
  - `idempotencyKey`

## Entity: BillingEquitySignal
- **Purpose**: Captures latest billing equity recommendation relevant to cancellation flow.
- **Identity**:
  - `policyTermId + occurredUtc` (or source event id)
- **Core fields**:
  - `equityPercentage`
  - `cancellationRecommended` (bool)
  - `source` (Billing)
  - `occurredUtc`

## Relationships
- One `Policy` can have many `PolicyLifecycleTermState` entities (one per term).
- One `PolicyLifecycleTermState` can have many `PolicyLifecycleMilestone` records.
- `BillingEquitySignal` entries map to one `PolicyLifecycleTermState` by `policyTermId`.

## Validation Rules
- `termTicks` MUST be > 0.
- Milestone percentages MUST be within `(0, 100]` and strictly ordered where multiple milestones exist.
- `renewalOpenPercent < termEndPercent`.
- `graceWindowPercent` MUST be `> 0` and `< 100`.
- `policyTermId` and `policyId` MUST be present on every workflow-driving message.

## State Transitions
- `Bound -> Issued -> Active`
- `Active -> PendingCancellation -> Cancelled` (if equity remains below threshold at recheck)
- `PendingCancellation -> Active` (if equity recovers before/at recheck)
- `Active -> Expired` (term end without accepted renewal)
- `Active/PendingRenewal -> Renewed` (renewal accepted)

## Concurrency and Idempotency
- Transition application uses optimistic concurrency on term state document.
- Duplicate transition/milestone messages are ignored using dedup keys.
- Out-of-order messages that violate valid transition rules are no-op with warning logs.
