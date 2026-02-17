# Policy Domain

**Bounded Context**: Policy Management | **Ports**: API=7077, Endpoint.In=7078

This bounded context manages insurance policy lifecycle from quote acceptance through cancellation and reinstatement.

## Architecture

### Responsibilities
- Create policies from accepted quotes (QuoteAccepted event subscriber)
- Issue policies (trigger billing via PolicyIssued event)
- Cancel policies with unearned premium calculation
- Reinstate cancelled policies
- Track policy status lifecycle

### Message Contracts

**Subscribes To** (from Rating & Underwriting):
- `QuoteAccepted` → Creates new policy in Bound status

**Publishes** (internal domain events):
- `PolicyBound` → When policy created from quote
- `PolicyCancelled` → When policy cancelled
- `PolicyReinstated` → When cancelled policy reinstated

**Publishes** (PublicContracts for cross-domain integration):
- `PolicyIssued` → Triggers Billing to create billing account

## Data Model

### Cosmos DB Configuration
- **Container**: `policy`
- **Partition Key**: `/policyId`
- **Document Types**: Policy (main entity), PolicyNumberCounter (sequence generator)

### Policy Entity
```csharp
{
  "id": "string (policyId)",
  "policyId": "string (partition key)",
  "policyNumber": "KWG-2026-000001",
  "quoteId": "string",
  "customerId": "string",
  "status": "Bound|Active|Cancelled|Expired",
  "effectiveDate": "DateTimeOffset",
  "expirationDate": "DateTimeOffset",
  "boundDate": "DateTimeOffset",
  "issuedDate": "DateTimeOffset?",
  "cancelledDate": "DateTimeOffset?",
  "structureCoverageLimit": decimal,
  "structureDeductible": decimal,
  "contentsCoverageLimit": decimal,
  "contentsDeductible": decimal,
  "termMonths": int,
  "premium": decimal,
  "unearnedPremium": "decimal?",
  "cancellationReason": "string?",
  "createdUtc": "DateTimeOffset",
  "updatedUtc": "DateTimeOffset",
  "_etag": "string (optimistic concurrency)"
}
```

### Policy Number Format
- Format: `KWG-YYYY-NNNNNN`
- Example: `KWG-2026-000001`
- Generated via counter document with optimistic concurrency
- Resets annually (new sequence per year)

## API Endpoints

| Method | Path | Description | Response |
|--------|------|-------------|----------|
| GET | `/api/policies/{policyId}` | Get policy by ID | 200 PolicyResponse / 404 |
| POST | `/api/policies/{policyId}/issue` | Issue bound policy (triggers billing) | 200 PolicyResponse / 404 / 409 |
| POST | `/api/policies/{policyId}/cancel` | Cancel active policy | 200 CancelPolicyResponse / 404 |
| POST | `/api/policies/{policyId}/reinstate` | Reinstate cancelled policy | 200 PolicyResponse / 404 / 409 |
| GET | `/api/policies/customers/{customerId}/policies` | List customer policies | 200 CustomerPoliciesResponse |

### Error Responses
- **404 PolicyNotFound**: `{ "error": "PolicyNotFound", "message": "..." }`
- **409 InvalidPolicyStatus**: `{ "error": "InvalidPolicyStatus", "message": "..." }`
- **400 ValidationFailed**: ASP.NET Core ProblemDetails format

## Projects

```
services/policy/
├── src/
│   ├── Api/                      # HTTP endpoints (port 7077)
│   │   ├── Controllers/          # PoliciesController (5 endpoints)
│   │   └── Models/               # Request/Response DTOs
│   ├── Domain/                   # Business logic
│   │   ├── Contracts/Events/     # PolicyBound, PolicyCancelled, PolicyReinstated
│   │   ├── Managers/             # PolicyManager (orchestrates operations)
│   │   ├── Models/               # Policy entity
│   │   ├── Repositories/         # Cosmos DB data access
│   │   └── Services/             # PolicyNumberGenerator
│   ├── Infrastructure/           # Technical implementations
│   │   ├── CosmosDbInitializer.cs
│   │   ├── CosmosSystemTextJsonSerializer.cs
│   │   └── NServiceBusConfigurationExtensions.cs
│   └── Endpoint.In/              # Message processing (port 7078)
│       └── Handlers/             # QuoteAcceptedHandler
└── test/
    ├── Unit.Tests/               # Domain logic tests (xUnit)
    └── Integration.Tests/        # API tests (Playwright)
```

## Dependencies

### External Dependencies
- **Platform**: RiskInsure.PublicContracts (PolicyIssued, QuoteAccepted events)
- **Infrastructure**: Azure Cosmos DB, Azure Service Bus

### Internal Dependencies
- Api → Domain → Infrastructure
- Endpoint.In → Domain + Infrastructure

## Local Development

### Prerequisites
- .NET 10 SDK
- Azure Cosmos DB instance (or emulator)
- Azure Service Bus namespace

### Configuration

**API** (`src/Api/appsettings.Development.json`):
```json
{
  "ConnectionStrings": {
    "ServiceBus": "Endpoint=sb://...",
    "CosmosDb": "AccountEndpoint=https://..."
  },
  "CosmosDb": {
    "DatabaseName": "RiskInsure",
    "ContainerName": "policy"
  }
}
```

**Endpoint.In** (`src/Endpoint.In/appsettings.Development.json`):
```json
{
  "ConnectionStrings": {
    "ServiceBus": "Endpoint=sb://...",
    "CosmosDb": "AccountEndpoint=https://..."
  },
  "CosmosDb": {
    "DatabaseName": "RiskInsure",
    "ContainerName": "policy"
  }
}
```

### Running Locally

**Terminal 1 - API**:
```powershell
cd services/policy/src/Api
dotnet run
# API starts on http://localhost:7077
# Scalar UI at http://localhost:7077/scalar/v1
```

**Terminal 2 - Endpoint.In**:
```powershell
cd services/policy/src/Endpoint.In
dotnet run
# Listens for QuoteAccepted events from Service Bus
```

### Testing

**Unit Tests** (PolicyManager, PolicyNumberGenerator):
```powershell
cd services/policy/test/Unit.Tests
dotnet test
# 5 tests covering idempotency, validation, business logic
```

**Integration Tests** (API endpoints with Playwright):
```powershell
cd services/policy/test/Integration.Tests

# First-time setup
npm install
npx playwright install chromium

# Run tests (requires API running on port 7077)
npm test                  # Headless
npm run test:ui          # Interactive UI (recommended)
npm run test:headed      # Browser visible
npm run test:debug       # Step-through debugger
npm run test:report      # View last results
```

## Key Patterns

### Idempotent Message Handling
```csharp
public async Task Handle(QuoteAccepted message, ...)
{
    // Check for existing policy by quoteId
    var existing = await _repository.GetByQuoteIdAsync(message.QuoteId);
    if (existing != null)
    {
        _logger.LogInformation("Policy already exists for quote {QuoteId}, skipping", message.QuoteId);
        return; // Safe to ignore duplicate
    }
    
    // Create new policy...
}
```

### Policy Number Generation with Optimistic Concurrency
```csharp
public async Task<string> GenerateNextAsync()
{
    var year = DateTimeOffset.UtcNow.Year;
    var counterId = $"policy-number-counter-{year}";
    
    for (int attempt = 0; attempt < 5; attempt++)
    {
        try
        {
            var counter = await _container.ReadItemAsync<PolicyNumberCounter>(...);
            counter.Resource.CurrentSequence++;
            
            // Use ETag for optimistic concurrency
            await _container.ReplaceItemAsync(counter.Resource, ..., 
                new ItemRequestOptions { IfMatchEtag = counter.Resource.ETag });
            
            return $"KWG-{year}-{counter.Resource.CurrentSequence:D6}";
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            // ETag mismatch - someone else updated, retry
            await Task.Delay((int)Math.Pow(2, attempt) * 100);
        }
    }
    
    throw new InvalidOperationException("Failed to generate policy number after 5 attempts");
}
```

### Unearned Premium Calculation (Pro-Rata)
```csharp
public async Task<Policy> CancelPolicyAsync(string policyId, DateTimeOffset cancellationDate, string reason)
{
    var policy = await _repository.GetByIdAsync(policyId);
    
    // Calculate unearned premium
    var totalDays = (policy.ExpirationDate - policy.EffectiveDate).TotalDays;
    var daysUsed = (cancellationDate - policy.EffectiveDate).TotalDays;
    var daysRemaining = totalDays - daysUsed;
    
    policy.UnearnedPremium = policy.Premium * (decimal)(daysRemaining / totalDays);
    policy.Status = "Cancelled";
    policy.CancelledDate = cancellationDate;
    policy.CancellationReason = reason;
    
    await _repository.UpdateAsync(policy);
    await _messageSession.Publish(new PolicyCancelled(...));
    
    return policy;
}
```

## Integration Points

### Upstream (Receives Events From)
- **Rating & Underwriting**: QuoteAccepted → Policy domain creates policy in Bound status

### Downstream (Publishes Events To)
- **Billing**: PolicyIssued → Billing creates billing account for new policy
- **Billing**: PolicyCancelled → Billing processes refund for unearned premium

## Troubleshooting

**API fails to start**:
- Verify port 7077 is available: `netstat -ano | findstr :7077`
- Check connection strings in appsettings.Development.json
- Ensure Cosmos DB and Service Bus are accessible

**Endpoint.In shows "Production requires..." error**:
- Verify launchSettings.json has `DOTNET_ENVIRONMENT=Development`
- Check appsettings.Development.json exists and has connection strings

**QuoteAccepted events not processed**:
- Verify Endpoint.In is running
- Check Service Bus queue `RiskInsure.Policy.Endpoint` exists
- Review NServiceBus logs for routing issues

**Policy number generation fails**:
- Check Cosmos DB container has partition key `/id` for counter document
- Review retry logic in logs (max 5 attempts)
- Verify optimistic concurrency (ETag) is working

**Integration tests fail**:
- Ensure API is running on port 7077 before running tests
- Check validation error assertions match actual API response format
- Review test README for setup instructions

## Constitutional Compliance

This domain follows all principles from [constitution.md](../../../.specify/memory/constitution.md):

- ✅ **I. Domain Language**: Uses "Policy", "Bound", "Issued", "Cancelled" consistently
- ✅ **II. Single-Partition Data Model**: `/policyId` partition key, all related data co-located
- ✅ **III. Atomic State Transitions**: ETags for optimistic concurrency on all updates
- ✅ **IV. Idempotent Message Handlers**: QuoteAcceptedHandler checks existing by quoteId
- ✅ **V. Structured Observability**: All logs include policyId/quoteId correlation fields
- ✅ **VI. Message-Based Integration**: QuoteAccepted (subscribe), PolicyIssued (publish)
- ✅ **VII. Thin Message Handlers**: QuoteAcceptedHandler delegates to PolicyManager
- ✅ **VIII. Test Coverage Requirements**: 5 unit tests, 5 integration test suites
- ✅ **IX. Technology Constraints**: .NET 10, NServiceBus 9.x, Cosmos DB, xUnit, Playwright
- ✅ **X. Naming Conventions**: Commands (verb+noun), Events (noun+past tense)

---

**Last Updated**: 2026-02-05  
**Version**: 1.0.0  
**Maintained By**: Policy Domain Team

