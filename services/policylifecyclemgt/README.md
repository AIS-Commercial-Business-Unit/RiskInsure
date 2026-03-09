# PolicyLifeCycleMgt Domain

Bounded context for policy lifecycle processing cloned from `services/policy` with lifecycle-specific domain language.

## Runtime

- API: `http://localhost:7079`
- Endpoint.In: message endpoint host (`RiskInsure.PolicyLifeCycleMgt.Endpoint`)
- Cosmos container: `policylifecycle`
- Partition key: `/policyId`

## Contracts

- Consumes: `QuoteAccepted` (`RiskInsure.PublicContracts.Events`)
- Publishes internal events:
  - `LifeCycleInitiated`
  - `LifeCycleCancelled`
  - `LifeCycleReinstated`
- Publishes public event (for backward compatibility during transition):
  - `PolicyIssued`

## Numbering

- Lifecycle number format: `LCM-{year}-{sequence:D6}`
- Counter document id format: `LifeCycleNumberCounter-{year}`
- Counter is stored in `policylifecycle` container and isolated from Policy service.

## API Endpoints

- `POST /api/lifecycles/{policyId}/issue`
- `GET /api/lifecycles/{policyId}`
- `GET /api/customers/{customerId}/lifecycles`
- `POST /api/lifecycles/{policyId}/cancel`
- `POST /api/lifecycles/{policyId}/reinstate`

## Traffic Gating for Parallel Run

`Endpoint.In` supports deterministic traffic split by `QuoteId`.

`src/Endpoint.In/appsettings.json`:

```json
"TrafficRouting": {
  "EnableLifeCycleProcessing": false,
  "LifeCycleProcessingPercentage": 0
}
```

- `EnableLifeCycleProcessing=false`: no `QuoteAccepted` messages are processed.
- `LifeCycleProcessingPercentage=10`: approximately 10% of quotes are routed to this service.
- Use ramp strategy: `0 -> 10 -> 50 -> 100`.

## Local Run

```powershell
# API
cd services/policylifecyclemgt/src/Api
dotnet run

# Endpoint.In
cd services/policylifecyclemgt/src/Endpoint.In
dotnet run

# Unit tests
cd services/policylifecyclemgt/test/Unit.Tests
dotnet test
```
