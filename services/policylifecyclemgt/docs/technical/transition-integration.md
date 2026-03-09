# PolicyLifeCycleMgt Transition Integration

## Event Strategy During Migration

- Keep `QuoteAccepted` as the consumed public contract.
- Publish internal lifecycle events for local domain workflows:
  - `LifeCycleInitiated`
  - `LifeCycleCancelled`
  - `LifeCycleReinstated`
- Continue publishing `PolicyIssued` from `RiskInsure.PublicContracts` to avoid breaking billing and other downstream consumers.

## Routing Strategy

Routing is controlled in `Endpoint.In` using `TrafficRouting` settings.

- No source-side dual publish required.
- Deterministic quote-based routing ensures stable behavior for repeated deliveries.

## Data Independence Checks

- All lifecycle documents and counters are created in `policylifecycle` container.
- `policyId` partition key is retained for consistency.
- Counter document id format: `LifeCycleNumberCounter-{year}`.

## Retirement Readiness

Policy service retirement can begin only when:

- `LifeCycleProcessingPercentage=100` is stable for agreed period
- No unresolved integration or data integrity incidents
- Billing confirms `PolicyIssued` continuity from `PolicyLifeCycleMgt`
