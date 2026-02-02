# GitHub Copilot Instructions for RiskInsure

## Repository Structure

RiskInsure is a monorepo with two top-level organizational folders:

- **`platform/`** - Cross-cutting concerns, shared contracts, infrastructure templates, and UI components
- **`services/`** - Business-specific bounded contexts (e.g., Billing, Payments, FileIntegration)

Each service in `services/` follows the multi-layer architecture defined in [copilot-instructions/project-structure.md](../copilot-instructions/project-structure.md).

---

## Architecture Principles

**Primary Reference**: See [copilot-instructions/constitution.md](../copilot-instructions/constitution.md) for non-negotiable architectural rules.

### Core Patterns

- **Event-Driven Architecture**: Services integrate through Azure Service Bus messages (commands and events)
- **Message-Based Integration**: NServiceBus 10 for all inter-service communication
- **Single-Partition Data Model**: Cosmos DB containers partitioned by processing unit (e.g., `/fileRunId`, `/orderId`)
- **Thin Message Handlers**: Validate input → delegate to domain service → publish events
- **Idempotent Operations**: All handlers safe to retry/replay
- **Domain-Centric**: Pure domain layer with zero infrastructure dependencies

---

## Technology Stack

### Required Versions
- **.NET**: 10.0
- **C#**: 13 with nullable reference types enabled
- **NServiceBus**: 10.x
- **xUnit**: Latest for testing

### Azure Services
- **Azure Cosmos DB**: Primary data store (NoSQL)
- **Azure Service Bus**: Message transport
- **Azure Container Apps**: NServiceBus endpoint hosting with KEDA scaling
- **Azure Logic Apps Standard**: Orchestration workflows and operational glue
- **Azure Blob Storage**: Large payload storage

---

## Project Structure

Each service follows a consistent multi-layer architecture:

```
services/
└── ServiceName/                    # Domain name (e.g., Billing, Payments)
    ├── src/
    │   ├── Api/                    # HTTP endpoints
    │   ├── Domain/                 # Pure business logic, message contracts
    │   ├── Infrastructure/         # Handlers, repositories, sagas
    │   └── Endpoint.In/            # NServiceBus hosting shell
    ├── test/
    │   ├── Api.Tests/
    │   ├── Domain.Tests/
    │   ├── Infrastructure.Tests/
    │   └── Endpoint.Tests/
    └── docs/
        └── domain-specific-standards.md
```

**See**: [copilot-instructions/project-structure.md](../copilot-instructions/project-structure.md) for complete structure and layer responsibilities.

---

## Dependency Rules

### Strict Layering (Inward Dependencies Only)

```
Api → Domain
Infrastructure → Domain  
Endpoint.In → Infrastructure → Domain
```

### Domain Layer (Zero Dependencies)

**The Domain layer**:
- ✅ Defines message contracts (commands/events as C# records)
- ✅ Defines domain models and business entities
- ✅ Defines service interfaces
- ✅ Contains business logic and validation rules
- ❌ **NEVER** references Infrastructure, API, or Endpoint layers
- ❌ **NEVER** references database SDKs (Cosmos, EF Core)
- ❌ **NEVER** references external service clients

**Rationale**: Pure business logic, testable without infrastructure, technology-agnostic.

---

## Message Contracts

### Contract Placement

**Internal Contracts** (within a single service):
- Place in the service's `Domain/Contracts` folder
- Only used by that specific service
- Not shared across service boundaries

**Public Contracts** (shared between services):
- **MUST** be placed in the `PublicContracts` project
- Used for events sent between domain services (including platform services)
- Currently referenced as a project reference
- Will ultimately become a NuGet package for sharing across repositories

**Example**:
- `InvoiceCreated` sent from Billing to Payments → `PublicContracts` project
- `PaymentInstructionReady` used only within FileIntegration → `FileIntegration.Domain/Contracts`

### Standard Fields

**All messages MUST include**:
- `MessageId` (Guid): Unique identifier for this message
- `OccurredUtc` (DateTimeOffset): When the event occurred
- `IdempotencyKey` (string): Deduplication key

**Processing messages SHOULD include**:
- Partition key identifier (e.g., `fileRunId`, `orderId`, `customerId`)
- Entity identifier when operating on specific entities
- Correlation fields for distributed tracing

### Naming Conventions

- **Commands**: `Verb` + `Noun` (e.g., `ProcessPayment`, `CreateInvoice`)
- **Events**: `Noun` + `VerbPastTense` (e.g., `PaymentProcessed`, `InvoiceCreated`)
- Use C# records for immutability
- Target `net10.0`

### Example

```csharp
namespace RiskInsure.Billing.Domain.Contracts;

public record InvoiceCreated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid InvoiceId,
    string CustomerId,
    decimal Amount,
    string IdempotencyKey
);
```

---

## Data Patterns

### Cosmos DB Single-Partition Strategy

- **One container** per domain (not per entity type)
- **Partition key** identifies processing unit (e.g., `/fileRunId`, `/orderId`, `/customerId`)
- **Document type discriminator** field distinguishes entity types
- **Co-located data**: All related documents in same partition
- **Free queries** within partition; cross-partition only for reporting

**Example**:
```csharp
public class InvoiceDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("orderId")]  // Partition key
    public Guid OrderId { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Invoice";
    
    // ... other properties
}
```

### Atomic State Transitions

When child entities transition states, parent aggregate counts **MUST** be updated atomically:
- Use optimistic concurrency (ETags)
- Update counts in same transaction (same partition)
- Log state transitions with before/after counts
- Retry on ETag mismatch

---

## Message Handlers

### Handler Responsibilities

**Handlers MUST be thin**:
1. Validate message structure (fast-fail)
2. Call domain service or repository
3. Publish resulting events
4. Log outcome

**Handlers MUST NOT**:
- ❌ Contain business logic (belongs in domain services)
- ❌ Access database directly (use repositories)
- ❌ Perform complex transformations
- ❌ Run long-running operations

### Idempotency Pattern

```csharp
public class ProcessEntityHandler : IHandleMessages<ProcessEntity>
{
    public async Task Handle(ProcessEntity message, IMessageHandlerContext context)
    {
        var existing = await _repository.GetByIdAsync(message.EntityId);
        if (existing != null)
        {
            _logger.LogInformation(
                "Entity {EntityId} already processed, skipping",
                message.EntityId);
            return; // Idempotent - safe to ignore
        }
        
        // Process new entity...
        await _service.ProcessAsync(message);
        
        await context.Publish(new EntityProcessed(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            EntityId: message.EntityId,
            IdempotencyKey: message.IdempotencyKey
        ));
    }
}
```

---

## Observability

### Structured Logging

**All logs MUST include**:
- Processing unit identifier (e.g., `fileRunId`, `orderId`)
- Entity identifier when operating on specific entities
- Correlation ID from NServiceBus message context
- Operation name

### Log Levels

- **Information**: State transitions, completion events, normal operations
- **Warning**: Retries, degraded conditions, non-critical issues
- **Error**: Failures requiring intervention, exceptions

### Example

```csharp
_logger.LogInformation(
    "Processing {EntityType} {EntityId} in context {ProcessingUnitId}",
    "Invoice", invoiceId, orderId);

_logger.LogWarning(
    "Retry attempt {Attempt} for {EntityType} {EntityId}",
    retryAttempt, "Payment", paymentId);

_logger.LogError(ex,
    "Failed to process {EntityType} {EntityId}: {ErrorMessage}",
    "Order", orderId, ex.Message);
```

---

## Code Quality Standards

### Build Configuration

All projects **MUST** include via `Directory.Build.props`:
```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <LangVersion>latest</LangVersion>
</PropertyGroup>
```

### Type Usage

- **Timestamps**: Use `DateTimeOffset` (not `DateTime`)
- **Identifiers**: 
  - `Guid` for system-generated identifiers
  - `string` for domain identifiers (customer IDs, order numbers)
- **Nullability**: Enable nullable reference types
- **Immutability**: Prefer records for DTOs and message contracts

---

## Testing Standards

### Coverage Targets

- **Domain Layer**: 90%+ coverage
- **Application Services**: 80%+ coverage
- **Message Handlers**: 80%+ coverage
- **Infrastructure**: Integration tests for repositories

### Test Patterns

- **Framework**: xUnit
- **Pattern**: AAA (Arrange-Act-Assert)
- **Naming**: `MethodName_Scenario_ExpectedResult`
- **Doubles**: Prefer fakes over mocks
- **Isolation**: Unit tests should not require infrastructure

### Example

```csharp
[Fact]
public async Task Handle_DuplicateMessage_DoesNotCreateDuplicate()
{
    // Arrange
    var existingEntity = new Entity { Id = "test-123" };
    _repository.Setup(r => r.GetByIdAsync("test-123"))
        .ReturnsAsync(existingEntity);
    
    var message = new ProcessEntity(
        MessageId: Guid.NewGuid(),
        OccurredUtc: DateTimeOffset.UtcNow,
        EntityId: "test-123",
        IdempotencyKey: "key-123"
    );
    
    // Act
    await _handler.Handle(message, _context.Object);
    
    // Assert
    _repository.Verify(r => r.CreateAsync(It.IsAny<Entity>()), Times.Never);
}
```

---

## Domain-Specific Standards

Each service MAY define domain-specific standards in `docs/domain-specific-standards.md`:

- Domain terminology (ubiquitous language)
- Prohibited terms
- Domain-specific message contracts
- State machines and transition rules
- Domain-specific validation rules

**Example**: See [platform/fileintegration/docs/filerun-processing-standards.md](../platform/fileintegration/docs/filerun-processing-standards.md) for FileIntegration domain rules.

---

## Naming Conventions

### Avoid Abbreviations

**Allowed abbreviations**:
- `Id` (Identifier)
- `Utc` (Coordinated Universal Time)
- `Uri` (Uniform Resource Identifier)
- Domain-specific acronyms (e.g., `Ach` for Automated Clearing House)

**All other names**: Use full words for clarity.

### Project Naming

- **Solution**: `ServiceName.sln` (e.g., `Billing.sln`)
- **Projects**: `ServiceName.LayerName.csproj` (e.g., `Billing.Domain.csproj`)
- **Handlers**: `MessageNameHandler.cs` (e.g., `ProcessPaymentHandler.cs`)
- **Repositories**: `IEntityRepository.cs` / `EntityRepository.cs`

---

## Related Documentation

- **[copilot-instructions/constitution.md](../copilot-instructions/constitution.md)** - Non-negotiable architectural rules (READ THIS FIRST)
- **[copilot-instructions/project-structure.md](../copilot-instructions/project-structure.md)** - Multi-layer project structure template
- **[docs/architecture.md](../docs/architecture.md)** - System architecture overview (if exists)
- **Service-specific standards**: `services/{ServiceName}/docs/domain-specific-standards.md`

---

## Quick Reference

### When creating a new service:
1. Create folder structure per [project-structure.md](../copilot-instructions/project-structure.md)
2. Start with Domain layer (contracts, models, interfaces)
3. Add Infrastructure layer (handlers, repositories)
4. Add API layer (HTTP endpoints)
5. Configure Endpoint.In (NServiceBus hosting)
6. **Add all projects to RiskInsure.slnx** using `dotnet sln add <path-to-csproj>`
7. Document domain-specific standards

### When adding a feature:
1. Review [constitution.md](../copilot-instructions/constitution.md) principles
2. Design messages (commands/events) first
3. Write tests (TDD encouraged)
4. Implement in Domain → Infrastructure → API order
5. Verify test coverage meets thresholds
6. Ensure naming conventions followed

### When reviewing code:
- ✅ Verify principles I-X compliance (constitution)
- ✅ Check dependency direction (always inward to Domain)
- ✅ Ensure handlers are thin and idempotent
- ✅ Verify test coverage meets thresholds
- ✅ Check naming conventions
- ✅ Ensure no prohibited technologies used
