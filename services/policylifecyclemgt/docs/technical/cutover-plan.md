# PolicyLifeCycleMgt Cutover Plan

## Rollout Steps

1. Set `EnableLifeCycleProcessing=false` and `LifeCycleProcessingPercentage=0`.
2. Deploy `PolicyLifeCycleMgt` API and Endpoint.In with monitoring enabled.
3. Enable processing at 10% and monitor for 24-48 hours.
4. Increase to 50% after stability checks pass.
5. Increase to 100% and keep `Policy` in read/stable mode until retirement criteria are met.

## Monitoring Signals

- Endpoint errors and retries
- Message processing latency (`QuoteAccepted` to persistence)
- `PolicyIssued` publish success and downstream billing consumption
- Cancellation/reinstatement success rates
- Lifecycle number generation gaps/conflicts

## Rollback Criteria

- Sustained elevated error rate above agreed SLO
- Data integrity issue in `policylifecycle` container
- `PolicyIssued` downstream integration breakages

## Rollback Action

- Set `EnableLifeCycleProcessing=false` (or percentage back to 0)
- Keep `Policy` active as authoritative path
- Investigate and patch before re-ramp
