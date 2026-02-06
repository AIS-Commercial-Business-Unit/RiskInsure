# Domain Builder Prompt

**Purpose**: Build a complete bounded context implementation with all layers and tests.

**Agent**: [domain-builder.agent.md](../agents/domain-builder.agent.md)

---

## Quick Start

```
@workspace Build the [DomainName] domain following the technical specification at services/[domain]/docs/technical/[domain]-technical-spec.md. Use the domain-builder agent to create all layers (API, Domain, Infrastructure, Endpoint.In) and integration tests.
```

---

## Full Invocation

### For Customer Domain

```
@workspace I need you to build the Customer domain implementation.

**Domain**: Customer
**Technical Spec**: services/customer/docs/technical/customer-technical-spec.md
**Business Requirements**: services/customer/docs/business/customer-management.md

Please use the domain-builder agent to:
1. Check for available ports (scan existing services)
2. Create complete project structure (Domain, Infrastructure, API, Endpoint.In, Tests)
3. Implement all 5 API endpoints from the technical spec
4. Create Playwright integration tests
5. Add projects to RiskInsure.slnx solution
6. Verify build and provide startup instructions

**Port Assignment**: Check and assign next available ports in 707X range

Follow all constitutional principles and architectural standards from copilot-instructions/.
```

---

## For Other Domains

### Rating & Underwriting

```
@workspace Build the RatingAndUnderwriting domain.

**Domain**: RatingAndUnderwriting
**Technical Spec**: services/ratingandunderwriting/docs/technical/rating-underwriting-technical-spec.md

Create:
- Quote entity with rating calculation logic
- Underwriting engine with Class A/B/Decline logic
- 6 API endpoints (start quote, submit underwriting, calculate, accept, get quote, list quotes)
- Background job for quote expiration
- Integration tests for quote lifecycle

Use domain-builder agent.
```

### Policy

```
@workspace Build the Policy domain.

**Domain**: Policy
**Technical Spec**: services/policy/docs/technical/policy-technical-spec.md

Create:
- Policy entity with lifecycle management
- QuoteAcceptedHandler (subscribes to QuoteAccepted event from Rating domain)
- PolicyNumberGenerator service
- 5 API endpoints (issue, get, list, cancel, reinstate)
- Integration tests for policy lifecycle

This domain SUBSCRIBES to events, so Endpoint.In will have handlers.

Use domain-builder agent.
```

### Billing (Multi-Policy)

```
@workspace Build the Billing domain with multi-policy support.

**Domain**: Billing
**Technical Spec**: services/billing/docs/technical/multi-policy-billing-technical-spec.md

Create:
- BillingAccount entity with multi-policy tracking
- PolicyIssuedHandler (subscribes to PolicyIssued event)
- FundsSettledHandler (subscribes to FundsSettled event)
- 3 API endpoints (record payment, get account, get payments)
- Integration tests for multi-policy scenarios

Use domain-builder agent.
```

---

## Step-by-Step Invocation

If you want more control over the process:

### Step 1: Port Check

```
@workspace Scan all existing services for port assignments and suggest next available ports for the [Domain] domain (need API port and Endpoint.In port).
```

### Step 2: Create Structure

```
@workspace Create the project structure for [Domain] domain:
- services/[domain]/src/{Api,Domain,Infrastructure,Endpoint.In}
- services/[domain]/test/{Unit.Tests,Integration.Tests}
- All .csproj files with correct namespaces and dependencies
```

### Step 3: Implement Domain Layer

```
@workspace Implement the Domain layer for [Domain]:
- Models from technical spec
- Event contracts
- Repository interfaces and Cosmos DB implementation
- Validation logic
- Manager orchestration
```

### Step 4: Implement API

```
@workspace Implement the API layer for [Domain]:
- Controllers with all endpoints from technical spec API table
- Request/Response DTOs
- Program.cs with DI, NServiceBus, Swagger
- Port [assigned-port]
```

### Step 5: Implement Endpoint.In

```
@workspace Implement the Endpoint.In layer for [Domain]:
- Message handlers (if any subscriptions)
- Program.cs with NServiceBus routing
- Port [assigned-port]
- IMPORTANT: Include launchSettings.json with DOTNET_ENVIRONMENT=Development
```

### Step 6: Create Tests

```
@workspace Create tests for [Domain]:
- Unit tests for validators, managers, repositories
- Playwright integration tests for all API endpoints
- README with run instructions
```

### Step 7: Verify

```
@workspace Verify the [Domain] implementation:
- Add projects to RiskInsure.slnx
- Build all projects
- Run unit tests
- Provide startup instructions
```

---

## Common Scenarios

### Domain with No Event Subscriptions (Customer)

```
@workspace Build Customer domain (API only, no inbound message handlers).

Technical spec: services/customer/docs/technical/customer-technical-spec.md

The Customer domain publishes events but doesn't subscribe to any, so:
- Endpoint.In project still needed for consistency
- Handlers folder will be empty
- Focus on API endpoints and unit tests
```

### Domain with Event Subscriptions (Policy)

```
@workspace Build Policy domain (subscribes to QuoteAccepted event).

Technical spec: services/policy/docs/technical/policy-technical-spec.md

The Policy domain:
- Subscribes to QuoteAccepted event from Rating & Underwriting
- Needs QuoteAcceptedHandler in Endpoint.In
- Publishes PolicyIssued event
```

### Domain with Multiple Handlers (Billing)

```
@workspace Build Billing domain (subscribes to PolicyIssued and FundsSettled events).

Technical spec: services/billing/docs/technical/multi-policy-billing-technical-spec.md

The Billing domain:
- Subscribes to PolicyIssued (from Policy)
- Subscribes to FundsSettled (from FundsTransferMgt)
- Needs two handlers: PolicyIssuedHandler, FundsSettledHandler
- Complex multi-policy data model
```

---

## Validation Commands

After agent completes, verify with:

### Build Check
```powershell
dotnet build services/[domain]/src/Api/Api.csproj
dotnet build services/[domain]/src/Endpoint.In/Endpoint.In.csproj
```

### Test Check
```powershell
dotnet test services/[domain]/test/Unit.Tests/Unit.Tests.csproj
```

### Port Check
```powershell
Get-ChildItem -Path services -Recurse -Filter "launchSettings.json" | 
  ForEach-Object { 
    Write-Host "`n$($_.Directory.Parent.Parent.Name):" -ForegroundColor Cyan
    Get-Content $_.FullName | Select-String "applicationUrl" 
  }
```

### Integration Test Check
```powershell
cd services/[domain]/test/Integration.Tests
npm install
npx playwright install chromium
npm run test:ui
```

---

## Troubleshooting Prompts

### Port Conflict
```
@workspace The agent assigned port [X] but it's already in use. Scan for available ports and update launchSettings.json for [Domain] API and Endpoint.In.
```

### Build Errors
```
@workspace The [Domain] build is failing with error: [error message]. Please fix the issue and verify build succeeds.
```

### Missing Dependencies
```
@workspace The [Domain] project is missing package references. Compare with billing/fundstransfermgt and add missing packages.
```

### launchSettings.json Missing
```
@workspace The [Domain] Endpoint.In project is missing Properties/launchSettings.json. Create it with DOTNET_ENVIRONMENT=Development to avoid "Production requires..." error.
```

### Integration Tests Failing
```
@workspace The Playwright tests for [Domain] are failing. Debug the API endpoints and fix the test scenarios.
```

---

## Reference Examples

### Customer Domain (Complete Example)
See the Customer domain implementation plan in the conversation history for a complete breakdown of:
- 52 files created across 5 layers
- Port assignments (API: 7073, Endpoint.In: 7076)
- 5 API endpoints
- 3 integration test files
- Full project structure

### Expected Output Structure
```
services/customer/
├── src/
│   ├── Api/                          (9 files)
│   ├── Domain/                       (16 files)
│   ├── Infrastructure/               (3 files)
│   └── Endpoint.In/                  (4 files)
└── test/
    ├── Unit.Tests/                   (4 files)
    └── Integration.Tests/            (6 files)
```

---

## Agent Configuration

The domain-builder agent follows:
- **Constitutional principles I-X** from copilot-instructions/constitution.md
- **Project structure template** from copilot-instructions/project-structure.md
- **API initialization** from copilot-instructions/init.api.md
- **Technical specification** from services/{domain}/docs/technical/

---

**Version**: 1.0.0  
**Last Updated**: February 5, 2026  
**See Also**: [domain-builder.agent.md](../agents/domain-builder.agent.md)
