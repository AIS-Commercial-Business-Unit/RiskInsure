# Customer Domain

Complete implementation of the Customer bounded context for RiskInsure.

## Overview

The Customer domain manages customer identity and contact information, providing CRUD operations for customer data and supporting GDPR compliance.

## Architecture

- **API Layer** (Port 7075): HTTP endpoints for customer operations
- **Domain Layer**: Core business logic, validation, and data models
- **Infrastructure Layer**: Cosmos DB persistence and NServiceBus configuration
- **Endpoint.In**: Message processing (currently no subscriptions)

## Port Assignments

- **API**: http://localhost:7075
- **Endpoint.In**: No HTTP port (NServiceBus only)

## Data Model

**Cosmos DB Container**: `customer`  
**Partition Key**: `/customerId`

### Customer Entity

- **Identity**: CustomerId (GUID), Email (unique), BirthDate, ZipCode
- **Contact Info**: FirstName, LastName, PhoneNumber, MailingAddress
- **Status**: Active, Inactive, Suspended, Closed
- **Preferences**: EmailVerified, MarketingOptIn, PreferredContactMethod

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/customers` | Create new customer |
| GET | `/api/customers/{id}` | Retrieve customer details |
| PUT | `/api/customers/{id}` | Update customer information |
| POST | `/api/customers/{id}/change-email` | Request email change |
| DELETE | `/api/customers/{id}` | Close account (GDPR) |

## Events Published

- **CustomerCreated**: When a new customer is created
- **CustomerInformationUpdated**: When customer details are updated
- **ContactInformationChanged**: When email/phone/address changes
- **CustomerClosed**: When account is closed

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
cd services/customer/src/Api
dotnet run
```

API will be available at: http://localhost:7075  
Swagger UI: http://localhost:7075/scalar/v1

### Start Endpoint.In

```powershell
cd services/customer/src/Endpoint.In
dotnet run
```

## Testing

### Unit Tests

```powershell
cd services/customer/test/Unit.Tests
dotnet test
```

**Coverage**: 19 tests covering validator and manager logic

### Integration Tests (Playwright)

```powershell
cd services/customer/test/Integration.Tests
npm install
npx playwright install chromium
npm run test:ui
```

**Note**: API must be running on port 7075 before running integration tests.

## GDPR Compliance

The `DELETE /api/customers/{id}` endpoint implements the "right to be forgotten":

1. Marks status as "Closed"
2. Anonymizes email (replaces with hashed value)
3. Clears all PII (FirstName, LastName, PhoneNumber, MailingAddress)
4. Retains CustomerId for transactional history

## Project Structure

```
services/customer/
├── src/
│   ├── Api/                  # HTTP endpoints
│   ├── Domain/              # Business logic, models, contracts
│   ├── Infrastructure/       # Cosmos DB, NServiceBus config
│   └── Endpoint.In/         # Message handlers (empty for now)
└── test/
    ├── Unit.Tests/          # Domain layer tests
    └── Integration.Tests/   # Playwright API tests
```

## Next Steps

1. Implement email verification workflow for email changes
2. Add integration with Policy domain (listen for PolicyIssued events)
3. Add customer search/filtering endpoints
4. Implement customer export for data portability (GDPR)
5. Add customer activity logging

## Dependencies

- Microsoft.Azure.Cosmos 3.53.1
- NServiceBus 9.2.6
- Scalar.AspNetCore 1.2.53
- xUnit 2.9.0
- Playwright (for integration tests)

## Build Status

✅ All projects build successfully  
✅ 19/19 unit tests passing  
✅ Integration tests configured and ready

---

**Version**: 1.0.0  
**Last Updated**: February 5, 2026
