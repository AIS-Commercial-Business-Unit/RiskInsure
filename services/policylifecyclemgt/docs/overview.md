# PolicyLifeCycleMgt Overview

## Purpose

`PolicyLifeCycleMgt` is a cloned bounded context from `Policy` for controlled parallel-run migration. It owns lifecycle processing for newly routed quote acceptances while `Policy` remains stable during transition.

## Scope

- Create lifecycle records from `QuoteAccepted`
- Issue lifecycle records and publish `PolicyIssued` for downstream compatibility
- Cancel and reinstate lifecycles
- Maintain lifecycle state and premium calculations
- Generate isolated lifecycle numbers (`LCM-*`) in dedicated Cosmos container

## Data Boundary

- Database: `RiskInsure`
- Container: `policylifecycle`
- Partition key: `/policyId`
- No reads from `policy` container

## Internal Events

- `LifeCycleInitiated`
- `LifeCycleCancelled`
- `LifeCycleReinstated`

## Public Integration

- Consumes: `QuoteAccepted`
- Publishes: `PolicyIssued` (kept for downstream compatibility during migration)

## Parallel-Run Control

`Endpoint.In` supports feature-flagged deterministic traffic split using `TrafficRouting` config:

- `EnableLifeCycleProcessing`
- `LifeCycleProcessingPercentage` (0-100)

This enables gradual rollout (`0% -> 10% -> 50% -> 100%`) and rollback without source-system dual-write changes.
