# Bounded Context - Project Structure Template

## Overview

This document describes the standard project structure for a bounded context in the platform. Each bounded context follows a multi-layer architecture using Azure Functions, NServiceBus messaging, and CosmosDB persistence.

## What is a Bounded Context?

A **bounded context** is a logical boundary around a specific business domain with:
- **Clear responsibility**: Single, well-defined business capability
- **Autonomous operation**: Can function independently of other contexts
- **Own data model**: Internal models optimized for its needs
- **Message contracts**: Integration points via commands and events
- **Independent deployment**: Can be deployed and scaled separately

Anytime you see Endpoint.In in this document that is another way of saying an endpoint that processes messages off Azure Service Bus

## Project Structure

```
DomainName/
├── src/
│   ├── Api/                          # HTTP API endpoints (Azure Functions)
│   ├── Domain/                       # Core business logic and contracts
│   ├── Infrastructure/               # Data access and external integrations
│   └── Endpoint/                     # Inbound message processing
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
├── Functions/
│   ├── EntityOperationFunction.cs       # Business operation endpoints
│   └── QueryFunction.cs                 # Query endpoints
├── Models/
│   ├── EntityRequest.cs                 # API request DTOs
│   └── EntityResponse.cs                # API response DTOs
├── Program.cs                           # DI container configuration
├── host.json                            # Azure Functions configuration
├── local.settings.json.template         # Environment variables template
└── Api.csproj                           # Project file
```

**Responsibilities**:
- HTTP endpoint handling (REST API)
- Request/response serialization
- Basic format validation (required fields, data types)
- **Synchronous work**: Call Domain managers directly (return 200/400/etc.)
- **Asynchronous work**: Publish commands to NServiceBus (return 202 Accepted)
- CORS configuration
- Authentication/authorization

**Dependencies**:
- `Domain` → Message contracts, managers, models
- `Microsoft.Azure.Functions.Worker`
- `NServiceBus.Extensions.DependencyInjection`

**Patterns**:
- ✅ **Sync**: Api → Manager (Domain) → Services → Database → Return result
- ✅ **Async**: Api → Publish Command → Return 202 → (Endpoint.In handles later)
- ❌ Business logic in functions (delegate to managers)
- ❌ Complex validation (belongs in domain)

---

### 2. Domain Layer

**Purpose**: Core business logic, domain models, and message contracts, calls to dependant services

**Key Files**:
```
Domain/
├── Contracts/
│   ├── Commands/
│   │   ├── RegisterBeneficiaryCommand.cs    # Commands (imperative)
│   │   └── UpdateBeneficiaryCommand.cs
│   ├── Events/
│   │   ├── BeneficiaryCreationSuccess.cs    # Events (past tense)
│   │   └── BeneficiaryCreationFailed.cs
│   └── IProvideCorrelationId.cs             # Shared interfaces
├── DTOs/
│   └── BeneficiaryRegistrationDto.cs        # Data transfer objects
├── Managers/
│   ├── IBeneficiaryManager.cs               # Manager interface
│   ├── BeneficiaryManager.cs                # Manager implementation
│   └── Services/
│       └── BeneficiaryIntegrationDatabase.cs # Database service implementation
├── Repositories/
│   ├── IBeneficiaryRepository.cs            # Repository interface
│   └── BeneficiaryRepository.cs             # Repository implementation (Cosmos DB)
├── Models/
│   ├── Beneficiary.cs                       # Domain models
│   └── BeneficiaryDocument.cs               # Cosmos DB document models
└── Domain.csproj
```

**Responsibilities**:
- Define message contracts (commands/events)
- Define domain models and business entities
- **Orchestrate business logic via Managers** (receive command → get data → update data → save data → publish event)
- Implement concrete services under Managers (e.g., BeneficiaryIntegrationDatabase)
- **Database access logic** (repositories using Cosmos DB)
- Define validation rules and business logic
- Define DTOs for data transfer

**Dependencies**:
- .NET BCL types
- NServiceBus.Contracts (for message contracts)
- **Microsoft.Azure.Cosmos** (for database access)
- Microsoft.Extensions.Logging (for logging)

**Dependency Rule**:
```
❌ Domain CANNOT reference:
   - Infrastructure (except for initialization/configuration)
   - API
   - Endpoints

✅ Domain CAN reference:
   - .NET BCL (System.*)
   - NServiceBus.Contracts
   - Microsoft.Azure.Cosmos (database access)
   - Microsoft.Extensions.Logging
```

**Rationale**:
- **Layered architecture** (not Clean Architecture)
- Managers orchestrate all business logic
- Domain owns its data access (repositories)
- Testable with emulators (Azurite, Cosmos DB Emulator)
- Single layer for business logic and data access

---

### 3. Infrastructure Layer

**Purpose**: Technical implementations, data access, external integrations.

**Key Files**:
```
Infrastructure/
├── CosmosDbInitializer.cs               # Database initialization/setup
├── NServiceBusConfigurationExtensions.cs # NServiceBus configuration helpers
├── queues.ps1                           # Queue setup scripts
└── Infrastructure.csproj
```

**Responsibilities**:
- Database initialization and configuration (CosmosDbInitializer)
- NServiceBus configuration extensions and helpers
- Shared configuration code used across projects
- Infrastructure setup scripts (queue creation, etc.)
- **NO business logic, handlers, or sagas**

**Dependencies**:
- `Domain` → For shared types if needed
- `Microsoft.Azure.Cosmos` (for initialization)
- `NServiceBus` (for configuration extensions)
- Configuration libraries

**Key Patterns**:
- **Initialization Pattern**: Set up infrastructure before use
- **Extension Methods**: Provide reusable configuration
- **Shared Configuration**: Used by Api and Endpoint.In projects

---

### 4. Endpoint.** Layer

**Purpose**: NServiceBus hosting shell for message processing.

**Key Files**:
```
Endpoint.**/
├── Handlers/
│   └── CreateBeneficiaryCommandHandler.cs # Message handlers
├── Sagas/
│   ├── BulkBeneficiaryUploadSaga.cs       # Saga implementations
│   └── BulkBeneficiaryUploadSagaData.cs   # Saga state
├── Program.cs                             # NServiceBus + DI configuration
├── host.json                              # Azure Functions config
├── local.settings.json.template           # Environment template
└── Endpoint.**.csproj
```

**Responsibilities**:
- **Contain message handlers** (Handlers folder)
- **Contain sagas** for workflow orchestration (Sagas folder)
- Handle messages and **delegate work to Domain managers**
- Configure NServiceBus routing and transport
- Configure saga persistence
- Configure retry and error policies
- Provide hosting environment (currently Azure Functions, future: Azure Container Apps/Docker)

**Dependencies**:
- `Domain` → Message contracts, managers, services
- `Infrastructure` → Configuration helpers (CosmosDbInitializer, NServiceBus extensions)
- `NServiceBus.Extensions.Hosting`
- `Microsoft.Azure.Functions.Worker`

**Important**: 
- Handlers delegate to Domain managers for business logic
- Handlers are thin - just message handling concerns
- Business logic lives in Domain managers

---

## Dependency Flow Diagram

```
                    ┌─────────────────────────────┐
                    │       API Layer             │
                    │  - HTTP endpoints           │
                    │  - Request validation       │
                    └──────┬──────────────┬───────┘
                           │              │
              Sync: Direct call      Async: Publish command
                           │              │
                           ↓              ↓
┌──────────────────────────┴──┐    ┌─────────────────────────────┐
│      Domain Layer           │    │   Endpoint.In Layer         │
│  - Managers (orchestrate)   │    │  - Handlers (delegate)      │
│  - Services (implement)     │←───│  - Sagas (orchestrate)      │
│  - Repositories (data)      │    └─────────────────────────────┘
│  - Message contracts        │              ↑
│  - Models                   │              │ Uses config
└─────────────┬───────────────┘              │
              │                    ┌─────────┴───────────┐
              │ Uses config        │ Infrastructure Layer│
              └────────────────────│  - CosmosDbInit     │
                                   │  - NServiceBus ext  │
                                   └─────────────────────┘

Flow Patterns:
1. Sync:  Api → Manager → Services → Database → Return result
2. Async: Api → Command → Endpoint.In (Handler) → Manager → Services → Database

Dependency Direction:
Api → Domain
Endpoint.In → Domain + Infrastructure (config only)
Infrastructure → Domain (for shared types)
```

---

## Dependency Rules

### Rule 1: Domain Contains Business Logic, Data Access, and external services

```
✅ ALLOWED:
- Reference .NET BCL (System.*)
- Reference NServiceBus.Contracts
- Reference Microsoft.Azure.Cosmos (for repositories)
- Reference Microsoft.Extensions.Logging
- Contain Managers (orchestrate business logic)
- Contain Services (concrete implementations)
- Contain Repositories (database access)
- HTTP Services

❌ FORBIDDEN:
- Reference Infrastructure, API, Endpoints
- Reference external service clients (except database)
```

**Why**: Layered architecture (not Clean Architecture) - Domain owns business logic and data access, testable with emulators.

---

### Rule 2: Infrastructure Provides Shared Configuration

```
Infrastructure → Domain (for shared types if needed)

✅ ALLOWED:
- Provide initialization code (CosmosDbInitializer)
- Provide configuration extensions (NServiceBusConfigurationExtensions)
- Reference Domain for shared types
- Reference database/messaging SDKs for setup

❌ FORBIDDEN:
- Contain business logic
- Contain handlers or sagas
- Contain repositories
- Reference API or Endpoint.In
```

**Why**: Shared configuration code reused by Api and Endpoint.In projects.

---

### Rule 3: API Depends on Domain (and optionally Infrastructure)

```
Api → Domain
Api → Infrastructure (for configuration helpers)

✅ ALLOWED:
- Reference Domain contracts, managers, models
- Call Domain managers directly (synchronous work)
- Publish Domain commands (asynchronous work)
- Reference Infrastructure for configuration

❌ FORBIDDEN:
- Reference Endpoint.In
- Contain business logic (delegate to managers)
- Direct database access (use managers/repositories)

⚠️ CHOOSE PATTERN:
- Sync work: Call manager, return result immediately
- Async work: Publish command, return 202 Accepted
```

**Why**: API can do both synchronous (via managers) and asynchronous (via commands) work depending on requirements.

---

### Rule 4: Endpoint.In Contains Handlers/Sagas, Depends on Domain and Infrastructure

```
Endpoint.In → Domain
Endpoint.In → Infrastructure (for configuration only)

✅ ALLOWED:
- **Contain handlers** in Handlers folder
- **Contain sagas** in Sagas folder
- Reference Domain contracts and managers
- Reference Infrastructure for configuration helpers
- Configure NServiceBus and DI
- Delegate work to Domain managers

❌ FORBIDDEN:
- Reference API
- Contain business logic (delegate to managers)
- Direct database access (use managers)
```

**Why**: Endpoint.In handles messages and delegates to Domain managers for business logic. Thin handlers keep message processing separate from business logic.

---

## Message Flow Patterns

### Synchronous Flow (Direct Call)
```
1. Client sends HTTP request to API
2. API validates format
3. API calls Domain manager directly
4. Manager orchestrates: get data → update → save → publish event
5. API returns result immediately (200/400/etc.)
```

### Asynchronous Flow (Command Publishing)
```
1. Client sends HTTP request to API
2. API validates format and publishes Command
3. API returns 202 Accepted immediately
4. NServiceBus routes Command to Endpoint.In
5. Handler receives Command, delegates to Manager
6. Manager orchestrates: get data → update → save → publish event
7. Other handlers process Event independently
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

### local.settings.json.template

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "CosmosDb__ConnectionString": "<<COSMOS_CONNECTION_STRING>>",
    "ServiceBus__ConnectionString": "<<ASB_CONNECTION_STRING>>"
  }
}
```

**⚠️ SECURITY**:
- Never commit `local.settings.json`
- Use templates with placeholders
- Replace placeholders locally
- Use Azure Key Vault for production

### host.json

```json
{
  "version": "2.0",
  "logging": {
    "logLevel": {
      "default": "Information",
      "Microsoft": "Warning"
    }
  },
  "extensions": {
    "http": {
      "routePrefix": "api"
    }
  }
}
```

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

### 3. Manager Pattern (Domain Orchestration)

```csharp
// Manager orchestrates business logic flow
public class BeneficiaryManager : IBeneficiaryManager
{
    private readonly IBeneficiaryRepository _repository;
    private readonly BeneficiaryIntegrationDatabase _database;
    private readonly IMessageSession _messageSession;
    
    public async Task CreateBeneficiaryAsync(RegisterBeneficiaryCommand command)
    {
        // 1. Get data (if needed)
        var existing = await _repository.GetByIdAsync(command.Id);
        
        // 2. Update/Create data
        var beneficiary = new Beneficiary(command);
        
        // 3. Save data
        await _database.SaveAsync(beneficiary);
        
        // 4. Publish event
        await _messageSession.Publish(new BeneficiaryCreationSuccess());
    }
}
```

### 4. Saga Pattern

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

### 5. Event-Driven Pattern

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

1. **Managers orchestrate** - Coordinate all business logic in Domain
2. **Handlers delegate** - Thin handlers in Endpoint.In call Domain managers
3. **Domain owns data** - Repositories in Domain layer (layered architecture)
4. **Choose sync vs async** - Direct manager calls (sync) or publish commands (async)
5. **One handler per message** - Single responsibility
6. **Separate concerns** - SignalR/notifications independent
7. **Test at each layer** - Unit tests for managers, integration for repositories
8. **Use commands for actions** - Events for notifications
9. **Use sagas for workflows** - Maintain state across steps
10. **Log appropriately** - Information for success, Warning for non-critical failures, Error for failures

### Don'ts ❌

1. **Don't put business logic in API** - Belongs in Domain managers
2. **Don't put business logic in handlers** - Delegate to Domain managers
3. **Don't put repositories in Infrastructure** - They belong in Domain
4. **Don't confuse with Clean Architecture** - This is layered architecture
5. **Don't ignore failures** - Throw in business handlers, catch in notification handlers
6. **Don't commit secrets** - Use templates
7. **Don't couple domains** - Use events for cross-domain communication
8. **Don't create circular dependencies** - Api and Endpoint.In both depend on Domain
9. **Don't skip tests** - Especially manager and repository tests
10. **Don't overcomplicate** - Start simple, add complexity when needed

---

## Port Assignment Convention

| Service | Port | Purpose |
|---------|------|---------|
| Domain API | 707X | HTTP endpoints (X = domain number) |
| Domain Endpoint.In | 707Y | Message processing (Y = X + 3) |

Example:
- Payments: API=7075, Endpoint=7074
- Medical: API=7071, Endpoint=7072
- Platform: API=7071, Endpoint=7072

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
1. Create Azure Functions for HTTP endpoints
2. Map HTTP requests to commands
3. Configure CORS
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

### Why Domain-Centric with Layered Architecture?
- **Business logic is central** - Managers orchestrate all work
- **Layered architecture** - Domain contains business logic AND data access
- **Testable with emulators** - Cosmos DB Emulator, Azurite for testing
- **Managers coordinate** - Clear orchestration pattern
- **Domain-driven design** - Business concepts directly in code

### Why Event-Driven?
- **Loose coupling** - Components don't know about each other
- **Extensibility** - Easy to add new functionality
- **Resilience** - Failures isolated to specific handlers
- **Scalability** - Process messages independently

### Why Separate Endpoints?
- **Independent scaling** - Scale message processing separately
- **Clear responsibility** - Endpoint.In hosts handlers, that's it
- **Deployment flexibility** - Deploy endpoints independently
- **Configuration isolation** - Different settings per endpoint

---

## Conclusion

This structure provides:
- ✅ **Layered architecture** (not Clean Architecture)
- ✅ **Domain owns business logic, data access, and service dependency access with Interfaces for testing**
- ✅ **Managers orchestrate** all business operations
- ✅ **Handlers delegate** to Domain managers
- ✅ **Infrastructure provides** shared configuration
- ✅ **Flexible patterns**: Synchronous (direct calls) and Asynchronous (commands)
- ✅ **Testable with emulators** (Cosmos DB, Azurite)
- ✅ **Event-driven patterns** for decoupling
- ✅ **Future-ready** (Docker container)

Use this as a template for creating new bounded contexts in the AcmeCorp platform.
