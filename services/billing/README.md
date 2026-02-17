# Billing Service

Insurance premium billing and payment tracking service.

## Architecture

Following the corrected [project-structure.md](../../../copilot-instructions/project-structure.md):

### Domain Layer (`src/Domain/`)
- **Models**: `BillingAccount` - Core billing entity
- **Services/BillingDb**: 
  - `IBillingAccountRepository` - Repository interface
  - `BillingAccountRepository` - Cosmos DB implementation with optimistic concurrency
  - `BillingAccountDocument` - Cosmos DB document model
- **Contracts/Commands**: `RecordPayment` - Internal command for payment recording
- **Dependencies**: Microsoft.Azure.Cosmos, Microsoft.Extensions.Logging.Abstractions

### Infrastructure Layer (`src/Infrastructure/`)
- Currently minimal - only configuration helpers if needed
- **NO** handlers, sagas, or repositories (per corrected architecture)

### Endpoint.In Layer (`src/Endpoint.In/`)
- **Handlers**: `RecordPaymentHandler` - Processes RecordPayment commands
- **Program.cs**: NServiceBus configuration with Azure Service Bus transport
- **Dependencies**: Domain, NServiceBus packages

### API Layer (`src/Api/`)
- **Controllers**: `BillingController` - POST /api/billing/payments (202 Accepted pattern)
- **Models**: `RecordPaymentRequest` - API request DTO
- **Program.cs**: ASP.NET Core Web API with NServiceBus (send-only)
- **Dependencies**: Domain, ASP.NET Core packages

## Data Model

**Cosmos DB Container**: `Billing`  
**Partition Key**: `/accountId`  
**Document Type**: `BillingAccount`

### BillingAccount Properties
- `AccountId` (string) - Primary key and partition key
- `CustomerId` (string) - Customer identifier
- `PolicyNumber` (string) - Insurance policy number
- `TotalPremiumDue` (decimal) - Total premium amount owed
- `TotalPaid` (decimal) - Total payments received
- `OutstandingBalance` (calculated) - TotalPremiumDue - TotalPaid
- `Status` (BillingAccountStatus enum) - Active, Suspended, PaidInFull, Cancelled
- `CreatedUtc`, `LastUpdatedUtc` (DateTimeOffset) - Timestamps
- `ETag` (string) - Optimistic concurrency control

## Message Contracts

### Commands (Internal)
- **RecordPayment**: Record payment to billing account
  - Fields: MessageId, OccurredUtc, AccountId, Amount, ReferenceNumber, IdempotencyKey

### Events (PublicContracts)
- **PaymentReceived**: Published when payment successfully applied
  - Fields: MessageId, OccurredUtc, AccountId, Amount, ReferenceNumber, TotalPaid, OutstandingBalance, IdempotencyKey

## Key Patterns

### Repository Pattern (Domain Layer)
- Interface defined in Domain (`IBillingAccountRepository`)
- Implementation in Domain (`BillingAccountRepository`)
- Uses Cosmos SDK with optimistic concurrency (ETag)
- `RecordPaymentAsync` has 3-retry loop for concurrency conflicts

### Thin Handler Pattern (Endpoint.In)
- Handler delegates to repository
- No business logic in handler
- Publishes events for downstream consumers

### 202 Accepted Pattern (API)
- API validates format
- Publishes command to NServiceBus
- Returns 202 Accepted immediately
- Endpoint.In processes asynchronously

### Atomic State Transitions
- `RecordPaymentAsync` applies payment with optimistic concurrency
- Retries on ETag mismatch (PreconditionFailed)
- Ensures accurate balance tracking

## Configuration

### API (`appsettings.Development.json`)
```json
{
  "ConnectionStrings": {
    "CosmosDb": "AccountEndpoint=https://localhost:8081/;AccountKey=...",
    "ServiceBus": "Endpoint=sb://your-namespace.servicebus.windows.net/;..."
  }
}
```

### Endpoint.In (`appsettings.Development.json`)
```json
{
  "ConnectionStrings": {
    "CosmosDb": "AccountEndpoint=https://localhost:8081/;AccountKey=...",
    "ServiceBus": "Endpoint=sb://your-namespace.servicebus.windows.net/;..."
  }
}
```

**Security**: Never commit `appsettings.Development.json` - use templates with placeholders

## Local Development

### Prerequisites
1. .NET 10 SDK
2. Cosmos DB Emulator or Azure Cosmos DB account
3. Azure Service Bus namespace

### Setup
1. Copy configuration templates:
   ```powershell
   cp src/Api/appsettings.Development.json.template src/Api/appsettings.Development.json
   cp src/Endpoint.In/appsettings.Development.json.template src/Endpoint.In/appsettings.Development.json
   ```

2. Update connection strings in `appsettings.Development.json` files

3. Create Cosmos DB container:
   ```powershell
   # Database: RiskInsure
   # Container: Billing
   # Partition key: /accountId
   ```

### Run API
```powershell
cd services/billing/src/Api
dotnet run
```
API available at: http://localhost:5000 (or configured port)

### Run Endpoint.In
```powershell
cd services/billing/src/Endpoint.In
dotnet run
```

### Test Flow
```powershell
# POST payment
curl -X POST http://localhost:5000/api/billing/payments `
  -H "Content-Type: application/json" `
  -d '{
    "accountId": "ACC-12345",
    "amount": 150.00,
    "referenceNumber": "PAY-001"
  }'

# Returns: 202 Accepted with confirmation JSON
# Endpoint.In processes command asynchronously
# PaymentReceived event published to Service Bus
```

## Testing

### Unit Tests (TODO)
- `Domain.Tests`: Test BillingAccount business logic
- `Infrastructure.Tests`: Integration tests with Cosmos emulator
- `Api.Tests`: Controller tests with mocked IMessageSession
- `Endpoint.Tests`: Handler tests with mocked repository

### Coverage Targets
- Domain: 90%+
- Application/Infrastructure: 80%+

## Related Documentation
- [Constitution](../../../.specify/memory/constitution.md) - Architectural principles
- [Project Structure](../../../copilot-instructions/project-structure.md) - Multi-layer architecture
- [Getting Started](../../../docs/getting-started.md) - Local development setup
