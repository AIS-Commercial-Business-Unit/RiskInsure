# Bounded Context - Project Structure Template

## Overview

This document describes the standard project structure for a bounded context in the RiskInsure platform. Each bounded context follows a multi-layer architecture using NServiceBus messaging and Cosmos DB persistence, hosted in Azure Container Apps.

## What is a Bounded Context?

A **bounded context** is a logical boundary around a specific business domain with:
- **Clear responsibility**: Single, well-defined business capability
- **Autonomous operation**: Can function independently of other contexts
- **Own data model**: Internal models optimized for its needs
- **Message contracts**: Integration points via commands and events
- **Independent deployment**: Can be deployed and scaled separately

## Project Structure

```
DomainName/
├── src/
│   ├── Api/                          # HTTP API endpoints 
│   ├── Domain/                       # Core business logic and contracts
│   ├── Infrastructure/               # Data access and external integrations
│   └── Endpoint.In/                  # Inbound message processing
├── test/
│   ├── Api.Tests/                    # API endpoint tests
│   ├── Domain.Tests/                 # Business logic tests
│   ├── Infrastructure.Tests/         # Repository and integration tests
│   └── Endpoint.Tests/               # Message handler tests
├── docs/                             # Domain documentation
│   ├── validation-rules.md           # Business validation rules
│   ├── architecture/                 # Architecture decisions
│   └── project-structure.md          # This document
└── DomainName.sln                    # Solution file
```

---

## Layer Responsibilities

### 1. Api Layer

**Purpose**: HTTP API endpoints for external clients.

**Key Files**:
```
Api/
├── Controllers/
│   ├── EntityController.cs              # Business operation endpoints
│   └── QueryController.cs               # Query endpoints
├── Models/
│   ├── EntityRequest.cs                 # API request DTOs
│   └── EntityResponse.cs                # API response DTOs
├── Program.cs                           # Application startup and DI configuration
├── appsettings.json                     # Application configuration
├── appsettings.Development.json.template # Local settings template
└── Api.csproj                           # Project file
```

**Responsibilities**:
- HTTP endpoint handling (REST API)
- Request/response serialization
- Basic format validation (required fields, data types)
- Publishing commands to NServiceBus
- CORS configuration
- Authentication/authorization

**Dependencies**:
- `Domain` → Message contracts and models
- `Microsoft.AspNetCore.OpenApi`
- `NServiceBus.Extensions.DependencyInjection`

**Anti-patterns to Avoid**:
- ❌ Business logic in controllers (use handlers instead)
- ❌ Direct database access (use commands)
- ❌ Complex validation (belongs in domain)

---

### 2. Domain Layer

**Purpose**: Core business logic, domain models, and message contracts. **Zero infrastructure dependencies**.

**Key Files**:
```
Domain/
├── Contracts/
│   ├── Commands/
│   │   ├── ExecuteBusinessOperation.cs  # Commands (imperative)
│   │   └── ProcessEntity.cs
│   ├── Events/
│   │   ├── OperationCompleted.cs        # Events (past tense)
│   │   └── ValidationFailed.cs
│   └── POCOs/
│       └── EntityData.cs                # Plain data objects
├── Models/
│   ├── Entity.cs                        # Core domain models
│   ├── ValidationResult.cs
│   └── EntityStatus.cs                  # Enumerations
├── Services/
│   ├── IValidator.cs                    # Service interfaces
│   └── IProcessor.cs
├── Exceptions/
│   └── DomainException.cs               # Domain-specific exceptions
└── Domain.csproj
```

**Responsibilities**:
- Define message contracts (commands/events)
- Define domain models and business entities
- Define validation rules and business logic
- Define service interfaces (implemented elsewhere)
- Define domain exceptions
- **NO** infrastructure concerns

**Dependencies**:
- **NONE** - Pure domain logic only
- .NET BCL types only
- NServiceBus.Contracts (for serialization attributes)

**Dependency Rule**:
```
❌ Domain CANNOT reference:
   - Infrastructure, API, Endpoints
   - Database SDKs (CosmosDB, EF Core)
   - External service clients
   - HTTP frameworks

✅ Domain CAN reference:
   - .NET BCL (System.*)
   - NServiceBus.Contracts attributes
```

**Rationale**:
- Pure business logic, technology-agnostic
- Testable without infrastructure
- Reusable across implementations
- Changes to tech stack don't affect domain
- Enables domain-driven design

---

### 3. Infrastructure Layer

**Purpose**: Technical implementations, data access, external integrations.

**Key Files**:
```
Infrastructure/
├── Repositories/
│   ├── IEntityRepository.cs             # Repository interface
│   ├── EntityRepository.cs              # Implementation
│   └── Models/
│       └── EntityDocument.cs            # Database document model
├── MessageHandlers/
│   ├── CommandHandlers/
│   │   └── ExecuteOperationHandler.cs   # Command handlers
│   ├── EventHandlers/
│   │   └── EntityProcessedHandler.cs    # Event handlers
│   └── SignalRHandlers/
│       └── ProgressNotificationHandler.cs # UI notifications (independent)
├── Sagas/
│   ├── BusinessProcessSaga.cs           # Workflow orchestration
│   └── BusinessProcessSagaData.cs       # Saga state
├── Services/
│   ├── BusinessService.cs               # Service implementations
│   └── ExternalServiceClient.cs         # External API clients
├── Configuration/
│   ├── DatabaseConfiguration.cs
│   └── MessagingConfiguration.cs
└── Infrastructure.csproj
```

**Responsibilities**:
- Implement repository interfaces from Domain
- Handle NServiceBus messages
- Orchestrate workflows with sagas
- Integrate with external services
- Manage data persistence
- Provide technical implementations

**Dependencies**:
- `Domain` → Contracts, models, interfaces
- `Microsoft.Azure.Cosmos` (or other database SDK)
- `NServiceBus`
- External service SDKs

**Key Patterns**:
- **Repository Pattern**: Abstract data access
- **Handler Pattern**: One handler per message
- **Saga Pattern**: Long-running workflows
- **Event-Driven**: Multiple handlers per event
- **Separation**: SignalR/notifications independent of business logic

---

### 4. Endpoint.In Layer

**Purpose**: NServiceBus hosting shell for message processing.

**Key Files**:
```
Endpoint.In/
├── Program.cs                           # NServiceBus configuration and startup
├── appsettings.json                     # Configuration settings
├── appsettings.Development.json.template # Environment template
└── Endpoint.In.csproj
```

**Responsibilities**:
- Host message handlers from Infrastructure
- Host sagas for workflow orchestration
- Configure NServiceBus routing and transport
- Configure saga persistence
- Configure retry and error policies
- Run as long-running process in Container Apps

**Dependencies**:
- `Domain` → Message contracts
- `Infrastructure` → Handlers and sagas
- `NServiceBus.Extensions.Hosting`
- `Microsoft.Extensions.Hosting`

**Important**: 
- Contains **NO business logic**
- Handlers live in Infrastructure
- Configuration is primary responsibility

---

## Dependency Flow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                      API Layer                              │
│  - HTTP endpoints                                           │
│  - Request validation                                       │
│  - Command publishing                                       │
└───────────────────────────┬─────────────────────────────────┘
                            │ NServiceBus Commands
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                   Endpoint.In Layer                         │
│  - Hosts handlers                                           │
│  - Hosts sagas                                              │
│  - NServiceBus configuration                                │
└───────────────────────────┬─────────────────────────────────┘
                            │ Invokes handlers
                            ↓
┌─────────────────────────────────────────────────────────────┐
│               Infrastructure Layer                          │
│  - Message handlers                                         │
│  - Sagas                                                    │
│  - Repositories                                             │
│  - External services                                        │
└───────────────────────────┬─────────────────────────────────┘
                            │ Uses contracts & interfaces
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                    Domain Layer                             │
│  - Message contracts (Commands/Events)                      │
│  - Domain models                                            │
│  - Business rules                                           │
│  - Service interfaces                                       │
│  - ✅ NO EXTERNAL DEPENDENCIES                              │
└─────────────────────────────────────────────────────────────┘

Dependency Direction: Always flows inward to Domain
Api → Domain
Infrastructure → Domain
Endpoint.In → Infrastructure → Domain
```

---

## Dependency Rules

### Rule 1: Domain Has Zero Dependencies

```
✅ ALLOWED:
- Reference .NET BCL (System.*)
- Reference NServiceBus.Contracts
- Define interfaces for other layers

❌ FORBIDDEN:
- Reference Infrastructure, API, Endpoints
- Reference database SDKs
- Reference external service clients
- Reference HTTP frameworks
```

**Why**: Pure business logic, testable without infrastructure, technology-agnostic.

---

### Rule 2: Infrastructure Depends ONLY on Domain

```
Infrastructure → Domain

✅ ALLOWED:
- Reference Domain contracts, models, interfaces
- Reference database SDKs
- Reference messaging frameworks
- Implement Domain interfaces

❌ FORBIDDEN:
- Reference API
- Reference Endpoint.In
- Direct references to other domains (use events)
```

**Why**: Technical implementations separate from business logic, easy to swap technologies.

---

### Rule 3: API Depends on Domain

```
Api → Domain

✅ ALLOWED:
- Reference Domain contracts
- Publish Domain commands
- Use Domain models

❌ FORBIDDEN:
- Reference Endpoint.In
- Contain business logic
- Direct database access

⚠️ PREFER:
- Publish commands over direct calls
- Keep API thin (HTTP concerns only)
```

**Why**: API is thin HTTP layer, business logic in handlers, decoupled processing.

---

### Rule 4: Endpoint.In Depends on Domain and Infrastructure

```
Endpoint.In → Domain
Endpoint.In → Infrastructure

✅ ALLOWED:
- Reference Domain contracts
- Reference Infrastructure handlers
- Configure NServiceBus
- Configure dependency injection

❌ FORBIDDEN:
- Reference API
- Contain business logic
- Contain handlers (they live in Infrastructure)
```

**Why**: Hosting shell for message processing, configuration-focused.

---

## Message Flow Patterns

### Command Flow
```
1. Client sends HTTP request to API
2. API validates format and publishes Command
3. NServiceBus routes Command to Endpoint.In
4. Handler processes Command
5. Handler publishes Event (success/failure)
6. Other handlers process Event independently
```

### Saga Orchestration Flow
```
1. Saga receives start message
2. Saga sends Commands for each step
3. Saga receives Event responses
4. Saga updates state and tracks progress
5. When complete, Saga publishes completion Event
6. Saga marks as complete
```

### Event-Driven Pattern
```
One Event → Multiple Independent Handlers

Business Handler → Updates records, triggers workflows
Notification Handler → Sends SignalR updates (failures don't affect business)
Audit Handler → Logs to audit system
Analytics Handler → Updates metrics
```

---

## Testing Strategy

### Domain Tests
- **No mocking required**
- Test pure business logic
- Fast execution
- No infrastructure dependencies

### Infrastructure Tests
- Integration tests with real database (emulator)
- Test repository implementations
- Test document mapping
- Slower but verify persistence

### API Tests
- Mock message session
- Test HTTP concerns
- Test request validation
- Test response codes

### Handler Tests
- Mock repositories and services
- Test message handling logic
- Test event publishing
- Test error handling

---

## Configuration Files

### appsettings.Development.json.template

```json
{
  "ConnectionStrings": {
    "CosmosDb": "<<COSMOS_CONNECTION_STRING>>",
    "ServiceBus": "<<ASB_CONNECTION_STRING>>"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "NServiceBus": "Information"
    }
  }
}
```

**⚠️ SECURITY**:
- Never commit `appsettings.Development.json`
- Use templates with placeholders
- Replace placeholders locally
- Use Azure Key Vault for production
- Use Managed Identity in Container Apps

---

## Common Patterns

### 1. Repository Pattern

```csharp
// Domain defines interface (what to do)
public interface IEntityRepository
{
    Task<Entity> GetByIdAsync(string id);
    Task SaveAsync(Entity entity);
}

// Infrastructure implements (how to do it)
public class EntityRepository : IEntityRepository
{
    private readonly Container _container;
    
    public async Task<Entity> GetByIdAsync(string id)
    {
        var doc = await _container.ReadItemAsync<EntityDocument>(id, ...);
        return MapToDomain(doc);
    }
}
```

### 2. Message Handler Pattern

```csharp
// One handler per message type
public class CommandHandler : IHandleMessages<ExecuteCommand>
{
    public async Task Handle(ExecuteCommand message, IMessageHandlerContext context)
    {
        // 1. Map to domain
        // 2. Validate
        // 3. Process
        // 4. Save
        // 5. Publish event
    }
}
```

### 3. Saga Pattern

```csharp
public class WorkflowSaga : Saga<SagaData>,
    IAmStartedByMessages<StartWorkflow>,
    IHandleMessages<StepCompleted>
{
    protected override void ConfigureHowToFindSaga(...)
    {
        // Map messages to saga using correlation ID
    }
    
    public async Task Handle(StartWorkflow message, ...)
    {
        // Initialize state
        // Send commands for each step
    }
    
    public async Task Handle(StepCompleted message, ...)
    {
        // Update state
        // Check completion
        // Publish final event if done
    }
}
```

### 4. Event-Driven Pattern

```csharp
// Multiple independent handlers for one event

public class BusinessHandler : IHandleMessages<EntityProcessed>
{
    // Critical business logic
}

public class NotificationHandler : IHandleMessages<EntityProcessed>
{
    // Send notifications (failure doesn't affect business)
    try {
        await _signalR.SendAsync(...);
    } catch {
        _logger.LogWarning("Notification failed");
        // Don't throw - business already succeeded
    }
}
```

---

## Best Practices

### Do's ✅

1. **Keep Domain pure** - No infrastructure dependencies
2. **Use interfaces** - Domain defines, Infrastructure implements
3. **One handler per message** - Single responsibility
4. **Separate concerns** - SignalR/notifications independent
5. **Test at each layer** - Unit tests for domain, integration for infrastructure
6. **Use commands for actions** - Events for notifications
7. **Keep API thin** - Just HTTP concerns
8. **Use sagas for workflows** - Maintain state across steps
9. **Log appropriately** - Information for success, Warning for non-critical failures, Error for failures
10. **Validate at boundaries** - API and domain both validate

### Don'ts ❌

1. **Don't put business logic in API** - Belongs in handlers
2. **Don't reference infrastructure from domain** - Violates separation
3. **Don't share data models** - Domain models ≠ Database documents
4. **Don't ignore failures** - Throw in business handlers, catch in notification handlers
5. **Don't commit secrets** - Use templates
6. **Don't couple domains** - Use events for cross-domain communication
7. **Don't create circular dependencies** - Always flow toward domain
8. **Don't mix concerns** - Each layer has one job
9. **Don't skip tests** - Especially domain tests
10. **Don't overcomplicate** - Start simple, add complexity when needed

---

## Local Development Ports

For local development, use these port conventions:

| Service | API Port | Notes |
|---------|----------|-------|
| Billing | 5001 | HTTP endpoints |
| Payments | 5002 | HTTP endpoints |
| FileIntegration | 5003 | HTTP endpoints |

**Note**: Endpoint.In projects run as console apps and don't expose HTTP ports.

---

## Creating a New Bounded Context

### Step 1: Define the Boundary
- What business capability does this serve?
- What is its single responsibility?
- What data does it own?
- How does it integrate with other contexts?

### Step 2: Create Project Structure
```bash
mkdir -p DomainName/src/{Api,Domain,Infrastructure,Endpoint.In}
mkdir -p DomainName/test/{Api.Tests,Domain.Tests,Infrastructure.Tests,Endpoint.Tests}
mkdir -p DomainName/docs
```

### Step 3: Start with Domain
1. Define message contracts (Commands/Events)
2. Define domain models
3. Define validation rules
4. Define service interfaces
5. Write domain tests

### Step 4: Build Infrastructure
1. Implement repository interfaces
2. Create message handlers
3. Create sagas (if needed)
4. Wire up external services
5. Write integration tests

### Step 5: Add API Layer
1. Create ASP.NET Core controllers for HTTP endpoints
2. Map HTTP requests to commands
3. Configure CORS and Swagger/OpenAPI
4. Add basic validation
5. Write API tests

### Step 6: Configure Endpoint
1. Set up NServiceBus configuration
2. Register handlers from Infrastructure
3. Configure saga persistence
4. Set up routing

### Step 7: Documentation
1. Document validation rules
2. Document message contracts
3. Document architecture decisions
4. Update this structure document

---

## Rationale for This Structure

### Why Layered Architecture?
- **Separation of concerns** - Each layer has one job
- **Testability** - Can test each layer independently
- **Flexibility** - Easy to swap implementations
- **Maintainability** - Changes isolated to specific layers

### Why Domain-Centric?
- **Business logic is central** - Protected from technical concerns
- **Pure and testable** - No infrastructure dependencies
- **Reusable** - Same domain logic across different implementations
- **Domain-driven design** - Business concepts directly in code

### Why Event-Driven?
- **Loose coupling** - Components don't know about each other
- **Extensibility** - Easy to add new functionality
- **Resilience** - Failures isolated to specific handlers
- **Scalability** - Process messages independently

### Why Separate Endpoints?
- **Independent scaling** - Scale message processing separately in Container Apps with KEDA
- **Clear responsibility** - Endpoint.In hosts handlers, that's it
- **Deployment flexibility** - Deploy endpoints independently as containers
- **Configuration isolation** - Different settings per endpoint

---

## Additional Resources

- **NServiceBus Patterns**: `Platform/docs/architecture/nservicebus-patterns.md`
- **Saga Patterns**: `Platform/docs/architecture/saga-patterns.md`
- **Testing Guide**: `Platform/docs/architecture/testing-guide.md`
- **Validation Patterns**: `Platform/docs/architecture/validation-patterns.md`

---

## Conclusion

This structure provides:
- ✅ Clear separation of concerns
- ✅ Testable architecture
- ✅ Flexible, extensible design
- ✅ Event-driven patterns
- ✅ Domain-driven design
- ✅ Independent deployments

Use this as a template for creating new bounded contexts in the RiskInsure platform.
