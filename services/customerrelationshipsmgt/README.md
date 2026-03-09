# CustomerRelationshipsMgt Domain

Complete implementation of the CustomerRelationshipsMgt bounded context for RiskInsure.

## Overview

The CustomerRelationshipsMgt domain manages customer relationships and contact information, providing CRUD operations for relationship data and supporting GDPR compliance. This is a new bounded context cloned from the Customer service with enhanced domain terminology focused on relationship management.

## Architecture

- **API Layer** (Port 7077): HTTP endpoints for relationship operations
- **Domain Layer**: Core business logic, validation, and data models
- **Infrastructure Layer**: Cosmos DB persistence and NServiceBus configuration
- **Endpoint.In**: Message processing (currently no subscriptions)

## Port Assignments

- **API**: http://localhost:7077
- **Endpoint.In**: No HTTP port (NServiceBus only)

## Data Model

**Cosmos DB Container**: `customerrelationships`  
**Partition Key**: `/customerId`

### Relationship Entity

- **Identity**: RelationshipId (CRM-{timestamp}), Email (unique), BirthDate, ZipCode
- **Contact Info**: FirstName, LastName, PhoneNumber, MailingAddress
- **Status**: Active, Inactive, Suspended, Closed
- **Preferences**: EmailVerified, MarketingOptIn, PreferredContactMethod

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/relationships` | Create new relationship |
| GET | `/api/relationships/{id}` | Retrieve relationship details |
| PUT | `/api/relationships/{id}` | Update relationship information |
| POST | `/api/relationships/{id}/change-email` | Request email change |
| DELETE | `/api/relationships/{id}` | Close account (GDPR) |

## Events Published

- **RelationshipCreated**: When a new relationship is created
- **RelationshipInformationUpdated**: When relationship details are updated
- **RelationshipContactInformationChanged**: When email/phone/address changes
- **RelationshipClosed**: When account is closed

## Validation Rules

- **Email**: Must be valid format and unique
- **Age**: Must be 18+ years old
- **Zip Code**: Must be 5-digit US postal code

## Running the Service

### Prerequisites

- .NET 10 SDK
- Cosmos DB (connection string in appsettings.Development.json)
- RabbitMQ (connection string in appsettings.Development.json)

### Start API

```powershell
cd services/customerrelationshipsmgt/src/Api
dotnet run
```

API will be available at: http://localhost:7077  
Swagger UI: http://localhost:7077/scalar/v1

### Start Endpoint.In

```powershell
cd services/customerrelationshipsmgt/src/Endpoint.In
dotnet run
```

## Testing

### Unit Tests

```powershell
cd services/customerrelationshipsmgt/test/Unit.Tests
dotnet test
```

**Coverage**: 19 tests covering validator and manager logic

### Integration Tests (Playwright)

```powershell
cd services/customerrelationshipsmgt/test/Integration.Tests
npm install
npx playwright install chromium
npm run test:ui
```

## Differences from Customer Service

This service is a clone of the Customer service with the following key changes:

1. **Terminology**: "Customer" → "Relationship" throughout domain language
2. **ID Prefix**: CUST-{timestamp} → CRM-{timestamp}
3. **API Routes**: `/api/customers` → `/api/relationships`
4. **Cosmos Container**: `customer` → `customerrelationships`
5. **Port**: 7075 → 7077
6. **Namespace**: `RiskInsure.Customer.*` → `RiskInsure.CustomerRelationshipsMgt.*`
7. **Endpoint Name**: `RiskInsure.Customer.*` → `RiskInsure.CustomerRelationshipsMgt.*`

## Data Independence

This service uses its own Cosmos DB container (`customerrelationships`) and does not share data with the Customer service. Both services can run in parallel during the transition period.

## Cutover Strategy

The plan is to gradually migrate traffic from Customer to CustomerRelationshipsMgt:

1. Run both services in parallel
2. Implement feature flag to route % of traffic to new service
3. Monitor error rates, latency, and event publishing
4. Gradually increase traffic: 0% → 10% → 50% → 100%
5. Retire Customer service once full cutover is complete

See [.github/prompts/plan-customerRelationshipsMgt.prompt.md](../.github/prompts/plan-customerRelationshipsMgt.prompt.md) for complete migration plan.
