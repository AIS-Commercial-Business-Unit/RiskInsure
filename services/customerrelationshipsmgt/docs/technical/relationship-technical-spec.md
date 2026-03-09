# CustomerRelationshipsMgt - Technical Specification

**Domain**: CustomerRelationshipsMgt  
**Version**: 1.0.0  
**Date**: March 2026

---

## Architecture Overview

The CustomerRelationshipsMgt service implements a clean architecture pattern with strict dependency rules:

```
Api → Domain ← Infrastructure
      ↑
Endpoint.In → Infrastructure → Domain
```

### Layer Responsibilities

- **Domain**: Pure business logic, contracts, validation, repositories (interfaces only)
- **Infrastructure**: Cosmos DB implementation, NServiceBus configuration, external integrations
- **Api**: HTTP endpoints, request/response models, API routing
- **Endpoint.In**: NServiceBus message handlers (currently none defined)

---

## Data Model

### Cosmos DB Schema

**Container Name**: `customerrelationships`  
**Partition Key**: `/customerId`  
**Database**: `RiskInsure`

### Relationship Document

```json
{
  "id": "CRM-1709654321000",
  "customerId": "CRM-1709654321000",
  "documentType": "Relationship",
  "email": "john.doe@example.com",
  "birthDate": "1990-05-15T00:00:00Z",
  "zipCode": "90210",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1-555-1234",
  "mailingAddress": {
    "street": "123 Main St",
    "city": "Beverly Hills",
    "state": "CA",
    "zipCode": "90210"
  },
  "status": "Active",
  "emailVerified": false,
  "marketingOptIn": false,
  "preferredContactMethod": "Email",
  "createdUtc": "2024-03-05T12:30:45Z",
  "updatedUtc": "2024-03-05T12:30:45Z",
  "_etag": "\"0000d886-0000-0200-0000-65e71d870000\""
}
```

**Partition Strategy**: Single partition per relationship using `/customerId` as partition key. All operations for a given relationship execute within the same logical partition, ensuring:
- Free queries within partition
- Atomic transactions
- No cross-partition queries needed
- Optimistic concurrency via ETags

---

## API Specification

**Base URL**: `http://localhost:7077/api/relationships` (Development)  
**Content-Type**: `application/json`

### POST /api/relationships

Create new relationship.

**Request Body**:
```json
{
  "firstName": "John",
  "lastName": "Doe",
  "email": "john.doe@example.com",
  "phone": "+1-555-1234",
  "address": {
    "street": "123 Main St",
    "city": "Beverly Hills",
    "state": "CA",
    "zipCode": "90210"
  },
  "birthDate": "1990-05-15T00:00:00Z"
}
```

**Response** (201 Created):
```json
{
  "relationshipId": "CRM-1709654321000",
  "email": "john.doe@example.com",
  "birthDate": "1990-05-15T00:00:00Z",
  "zipCode": "90210",
  "firstName": "John",
  "lastName": "Doe",
  "phone": "+1-555-1234",
  "address": {
    "street": "123 Main St",
    "city": "Beverly Hills",
    "state": "CA",
    "zipCode": "90210"
  },
  "status": "Active",
  "emailVerified": false,
  "createdUtc": "2024-03-05T12:30:45Z",
  "updatedUtc": "2024-03-05T12:30:45Z"
}
```

**Error Responses**:
- `400 Bad Request`: Validation failure (duplicate email, age < 18, invalid zip)

---

### GET /api/relationships/{relationshipId}

Retrieve relationship by ID.

**Response** (200 OK): Same structure as POST response

**Error Responses**:
- `404 Not Found`: RelationshipId does not exist

---

### PUT /api/relationships/{relationshipId}

Update relationship information.

**Request Body** (all fields optional):
```json
{
  "firstName": "Jane",
  "lastName": "Smith",
  "phoneNumber": "+1-555-5678",
  "mailingAddress": {
    "street": "456 Oak Ave",
    "city": "Los Angeles",
    "state": "CA",
    "zipCode": "90001"
  }
}
```

**Response** (200 OK): Updated relationship object

**Error Responses**:
- `404 Not Found`: RelationshipId does not exist

---

### DELETE /api/relationships/{relationshipId}

Close relationship account (GDPR compliance).

**Response** (204 No Content): Account closed and personal data anonymized

**Error Responses**:
- `404 Not Found`: RelationshipId does not exist

---

## Message Contracts

### RelationshipCreated

```csharp
public record RelationshipCreated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string RelationshipId,
    string Email,
    DateTimeOffset BirthDate,
    string ZipCode,
    string? FirstName,
    string? LastName,
    string IdempotencyKey
);
```

### RelationshipInformationUpdated

```csharp
public record RelationshipInformationUpdated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string RelationshipId,
    Dictionary<string, object> ChangedFields,
    string IdempotencyKey
);
```

### RelationshipClosed

```csharp
public record RelationshipClosed(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string RelationshipId,
    string IdempotencyKey
);
```

---

## Configuration

### appsettings.json Structure

```json
{
  "Messaging": {
    "MessageBroker": "RabbitMQ"
  },
  "ConnectionStrings": {
    "CosmosDb": "AccountEndpoint=...",
    "RabbitMQ": "host=localhost;username=guest;password=guest"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "NServiceBus": "Information"
    }
  }
}
```

---

## Testing Strategy

### Unit Tests (xUnit)

- RelationshipManager: Create, Update, Close operations
- RelationshipValidator: Email, ZipCode, Age validation rules
- RelationshipRepository: Cosmos DB CRUD operations (mocked)

**Target**: 80%+ code coverage on Domain layer

### Integration Tests (Playwright)

- POST /api/relationships - valid and invalid scenarios
- GET /api/relationships/{id} - retrieve and 404 cases
- PUT /api/relationships/{id} - update operations
- DELETE /api/relationships/{id} - GDPR deletion

**Target**: All API endpoints covered with positive and negative test cases

---

## Deployment

### Local Development

1. Start Cosmos DB Emulator
2. Start RabbitMQ (Docker): `docker run -d -p 5672:5672 -p 15672:15672 rabbitmq:3-management`
3. Configure `appsettings.Development.json` with connection strings
4. Run API: `dotnet run --project services/customerrelationshipsmgt/src/Api`
5. Run Endpoint.In: `dotnet run --project services/customerrelationshipsmgt/src/Endpoint.In`

### Azure Container Apps

- Api: HTTP ingress enabled on port 80
- Endpoint.In: Internal only, no HTTP ingress
- Environment variables: CosmosDb connection, RabbitMQ connection
- Managed identity for Cosmos DB authentication (future)

---
