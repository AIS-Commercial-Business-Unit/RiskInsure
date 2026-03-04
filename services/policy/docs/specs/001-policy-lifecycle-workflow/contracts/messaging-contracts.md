# Messaging Contracts: Policy Lifecycle Workflow

## Commands

### StartPolicyLifecycle
- Starts a new policy term saga instance.
- Required fields:
  - `MessageId`
  - `OccurredUtc`
  - `IdempotencyKey`
  - `PolicyId`
  - `PolicyTermId`
  - `TermTicks`
  - `EffectiveDate`
  - `ExpirationDate`

### ApplyPolicyLifecycleMilestone
- Applies computed milestone transition (`RenewalOpen`, `RenewalReminder`, `TermEnd`, `GraceRecheck`).
- Required fields:
  - `MessageId`
  - `OccurredUtc`
  - `IdempotencyKey`
  - `PolicyId`
  - `PolicyTermId`
  - `MilestoneType`
  - `MilestoneUtc`

### ProcessPolicyEquityUpdate
- Processes billing equity signal and evaluates cancellation path.
- Required fields:
  - `MessageId`
  - `OccurredUtc`
  - `IdempotencyKey`
  - `PolicyId`
  - `PolicyTermId`
  - `EquityPercentage`
  - `CancellationThresholdPercentage`

## Events

### PolicyRenewalWindowOpened
- Published when renewal window milestone is reached.
- Required fields:
  - `MessageId`
  - `OccurredUtc`
  - `IdempotencyKey`
  - `PolicyId`
  - `PolicyTermId`
  - `RenewalWindowStartUtc`

### PolicyEquityLevelReported
- Published/consumed as billing-equity signal for cancellation recommendation.
- Required fields:
  - `MessageId`
  - `OccurredUtc`
  - `IdempotencyKey`
  - `PolicyId`
  - `PolicyTermId`
  - `EquityPercentage`
  - `CancellationThresholdPercentage`
  - `CancellationRecommended`

### PolicyTermCompleted
- Published when current term workflow reaches terminal state.
- Required fields:
  - `MessageId`
  - `OccurredUtc`
  - `IdempotencyKey`
  - `PolicyId`
  - `PolicyTermId`
  - `CompletionStatus` (`Cancelled|Expired|Renewed`)
  - `CompletedUtc`

### PolicyRenewalAccepted
- Indicates renewal accepted and next term data available.
- Required fields:
  - `MessageId`
  - `OccurredUtc`
  - `IdempotencyKey`
  - `PolicyId`
  - `CurrentPolicyTermId`
  - `NextPolicyTermId`
  - `NextTermEffectiveDate`
  - `NextTermExpirationDate`

## Correlation
- Primary saga correlation: `PolicyTermId`
- Cross-term linkage: `PolicyId`

## Idempotency and Ordering
- Deduplicate by `IdempotencyKey` and transition keys (`PolicyTermId + MilestoneType`).
- Ignore stale equity/milestone updates older than latest processed transition timestamp for the term.
