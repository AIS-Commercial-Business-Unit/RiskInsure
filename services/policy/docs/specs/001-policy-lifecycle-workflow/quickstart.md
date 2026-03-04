# Quickstart: Policy Lifecycle Workflow

## Purpose
Implement and validate the policy term lifecycle workflow with saga orchestration, percentage-based milestones, renewal handoff, and billing-equity-driven cancellation recommendations.

## Prerequisites
- .NET 10 SDK
- Docker Desktop (for local RabbitMQ/Cosmos if required)
- `appsettings.Development.json` configured for Policy service API and Endpoint.In

## 1. Build
From repository root:

```powershell
dotnet restore
dotnet build
```

## 2. Run Policy API and Endpoint
Terminal 1:

```powershell
cd services/policy/src/Api
dotnet run
```

Terminal 2:

```powershell
cd services/policy/src/Endpoint.In
dotnet run
```

## 3. Start a policy lifecycle term
POST to API:

```http
POST /api/policies/{policyId}/lifecycle/start
Content-Type: application/json

{
  "policyTermId": "term-2025-01",
  "effectiveDateUtc": "2025-01-01T00:00:00Z",
  "expirationDateUtc": "2026-01-01T00:00:00Z",
  "termTicks": 12,
  "milestonePercentages": {
    "renewalOpenPercent": 66,
    "renewalReminderPercent": 83,
    "termEndPercent": 100
  },
  "cancellationThresholdPercentage": -20,
  "graceWindowPercent": 10
}
```

Expected result: `202 Accepted` and term lifecycle saga initialized.

## 4. Submit billing equity signal
POST to API:

```http
POST /api/policies/{policyId}/lifecycle/equity-update
Content-Type: application/json

{
  "policyTermId": "term-2025-01",
  "equityPercentage": -25,
  "cancellationThresholdPercentage": -20,
  "occurredUtc": "2025-05-15T13:00:00Z"
}
```

Expected result: `202 Accepted`; workflow enters pending cancellation path when threshold is breached.

## 5. Query lifecycle state
GET:

```http
GET /api/policies/{policyId}/lifecycle/terms/{policyTermId}
```

Validate:
- Current status and flags
- Pending cancellation start and grace recheck timestamps (if applicable)
- Completion status for terminal outcomes (`Cancelled`, `Expired`, or `Renewed`)

## 6. Run tests
From repository root (or targeted Policy test projects):

```powershell
dotnet test
```

Focus assertions:
- Milestone transitions computed from percentages
- Idempotent processing for duplicate commands/events
- Equity breach triggers pending cancellation then grace-window recheck
- Renewal accepted starts next term workflow without blocking current term completion
