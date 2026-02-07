# Domain Scaffolder Agent

**Purpose**: Create initial documentation structure for new bounded contexts (domains) in the RiskInsure solution from DDD specification files.

**Agent Type**: Documentation Structure Generation from DDD Specifications

---

## Capabilities

This agent **CREATES** the foundational documentation structure for new domains:
- ✅ Parse DDD specification files from `services/.rawservice/` directory
- ✅ Create domain folder in `services/` directory
- ✅ Generate documentation structure (`docs/overview.md`, `docs/business/`, `docs/technical/`)
- ✅ Create business requirements from DDD elements (Commands, Events, Policies)
- ✅ Create technical specifications with API endpoints, message contracts, and handlers
- ✅ Map DDD concepts to RiskInsure architecture patterns
- ✅ Add README.md with domain introduction
- ✅ Follow naming conventions and architectural standards

**This agent does NOT**:
- ❌ Create source code (src/ folders)
- ❌ Create test projects
- ❌ Add projects to solution file
- ❌ Generate implementation code

**For full domain implementation**, use the `domain-builder` agent after documentation is complete.

---

## Prerequisites

- DDD specification files exist in `services/.rawservice/` directory
- Files named: `{Context}_Systems_single_context_final.md`
- RiskInsure repository structure exists

---

## Input Requirements

The agent automatically discovers DDD specification files in `services/.rawservice/`:
```
services/.rawservice/
├── Sales_Systems_single_context_final.md
├── Billing_Systems_single_context_final.md
└── Shipping_Systems_single_context_final.md
```

**Or** specify domains manually:
```
Domain Names: NsbSales, NsbBilling, NsbShipping
```

---

## DDD Specification Format

### Expected Structure

```markdown
# **Contract: {ContractName} Systems**

## **Context: {ContextName}**

### **Context Elements for {ContextName}**

#### **Personas for {ContextName}**
- **Persona: {PersonaName}**

##### **Modules for {ContextName}**
- **Module: {ModuleName}**
  - **Views for Module {ModuleName}**
    - **View: {ViewName}**
      - *Data Elements*
        - {Field}: {Type}
  - **Policies for Module {ModuleName}**
    - **Policy: {PolicyName}**
      - *Policy Meta*
        - Handler: {HandlerName}
        - Parameters: {Params}
  - **Units of Work for Module {ModuleName}**
    - **Unit of Work: {UnitOfWorkName}**
      - **Commands for UoW {UnitOfWorkName}**
        - **Command: {CommandName}**
          - *Parameters*
            - {ParamName}: {Type}
      - **Events for UoW {UnitOfWorkName}**
        - **Event: {EventName}**
          - *Event Meta*
            - Published: Yes/No
          - *Data Elements*
            - {Field}: {Type}

#### **Context Relationships**

##### **Policy Relationships**
- **Policy [1-1]**
  - *Policy*: {PolicyName}
  - *Command: {CommandName}*
  - *External Subscriptions*
    - {ContextName}.{EventName}
```

---

## DDD to RiskInsure Mapping

### Architectural Translation

| **DDD Concept** | **RiskInsure Location** | **Notes** |
|----------------|------------------------|-----------|
| **Context** | `services/{contextname}/` | Becomes a bounded context domain |
| **Command** | `Domain/Contracts/Commands/{Command}.cs` | Internal command (C# record) |
| **Event (Published: Yes)** | `PublicContracts/Events/{Event}.cs` | Public event for cross-domain communication |
| **Event (Published: No)** | `Domain/Contracts/Events/{Event}.cs` | Internal event (domain-only) |
| **Policy + External Subscription** | `Endpoint.In/Handlers/{Event}Handler.cs` | Message handler subscribing to external event |
| **Policy + Local Command** | API triggers command internally | Via API endpoint or manager |
| **Unit of Work** | `Domain/Managers/{Domain}Manager.cs` method | Manager method implementing business logic |
| **View** | `Api/Controllers/{Entity}Controller.cs` | API endpoint (only if domain has Views) |
| **Module** | Logical grouping within domain | Documentation organization |
| **Persona** | External actor | Documented in business requirements |

### Event Flow Pattern

```
Entry Domain (has View/API)
└─ POST /api/{entity} → Command → Event ✓ Published to PublicContracts
     │
     ├──> Other Domain subscribes (EventHandler in Endpoint.In)
     │    └─ Command → Event ✓ Published
     │         │
     │         └──> Next Domain subscribes...
     │
     └──> Parallel Domain subscribes (EventHandler)
          └─ Command → Event ✓ Published
```

### Key Mapping Rules

1. **API Endpoints**: Only created for domains with **Views** (user-facing entry points)
2. **Partition Key**: Use `/{primaryId}` (e.g., `/orderId`) shared across related documents
3. **Document Models**: Each domain has its own document types with domain-specific ID + shared correlation ID
4. **Events**: All events with `Published: Yes` → `PublicContracts` project
5. **Handlers**: Each **External Subscription** → message handler in `Endpoint.In/Handlers/`
6. **Managers**: One manager per domain with methods matching Units of Work
7. **Validation**: Minimal validation (required fields, basic format checks)
8. **State Machines**: Only document what's explicitly in DDD spec (don't infer)

---

## Implementation Plan

The agent **EXECUTES** this structured approach:

### Phase 1: DDD Specification Discovery & Parsing

#### Step 1: Discover DDD Files
1. Scan `services/.rawservice/` for `*_Systems_single_context_final.md` files
2. Extract context names from file names (e.g., `Sales_Systems_*` → `Sales`)
3. Normalize to lowercase with prefix (e.g., `Sales` → `nsbsales`)

#### Step 2: Parse DDD Structure
For each specification file, extract:

**Context Information**:
- Context name (domain name)
- Description
- Version

**Units of Work** (become Manager methods):
- Unit of Work name
- Commands (with parameters)
- Events (with data elements)
- Logic details

**Policies** (become message handlers):
- Policy name
- Handler name
- External subscriptions (events from other contexts)
- Command triggered

**Views** (become API endpoints):
- View name
- Data elements displayed
- Associated policies

**Event Flow Mapping**:
- Which events are published (Published: Yes)
- Which external events are subscribed to
- Command-Event-Handler relationships

#### Step 3: Build Domain Model
From parsed data, create:
```json
{
  "domainName": "nsbsales",
  "contextName": "Sales",
  "hasApi": true,  // Has Views
  "commands": [
    { "name": "PlaceOrder", "parameters": ["OrderID: {GUID}"], "unitOfWork": "PlacingOrder" }
  ],
  "events": [
    { "name": "OrderPlaced", "published": true, "dataElements": ["OrderID: {GUID}"] }
  ],
  "handlers": [],  // No external subscriptions
  "entities": ["Order"],
  "correlationId": "OrderID"
}
```

### Phase 2: Generate Documentation Structure

#### For Each Domain

##### 1. Create Directory Structure
```
services/{domain}/
├── README.md
└── docs/
    ├── overview.md
    ├── business/
    │   └── {domain}-management.md
    └── technical/
        └── {domain}-technical-spec.md
```

##### 2. Generate README.md
Populate from DDD data:
- Context name as title
- Purpose from Context description
- Status: 📋 Planning
- Links to docs

**Template**:
```markdown
# {ContextName} Domain

**Status**: 📋 Planning

## Purpose
{Context Description from DDD}

## Documentation
- [Overview](docs/overview.md) - Domain overview and bounded context definition
- [Business Requirements](docs/business/{domain}-management.md) - Business rules and requirements
- [Technical Specification](docs/technical/{domain}-technical-spec.md) - Technical design and API contracts

## Domain Properties
- **Entry Point**: {API if has Views, or "Message-driven only"}
- **Primary Entity**: {Main entity name}
- **Correlation ID**: {Primary identifier}

## Integration
- **Events Published**: {List of published events}
- **Events Subscribed**: {List of external subscriptions}

---
*Generated from DDD specification: `services/.rawservice/{Context}_Systems_single_context_final.md`*
```

##### 3. Generate docs/overview.md
Populate from DDD elements:

**Template**:
```markdown
# {ContextName} Domain Overview

## Bounded Context
This domain implements the **{ContextName}** capability within the RiskInsure system.

**Context Boundary**:
- **IN SCOPE**: {List responsibilities from Units of Work}
- **OUT OF SCOPE**: {Other domain responsibilities}

## Core Responsibilities
{Generate from Units of Work - each UoW is a responsibility}

## Core Entities
{Generate from Commands/Events - extract entity names}

## Domain Events Published
{List events with Published: Yes}

| Event Name | Trigger | Data Elements | Consumers |
|------------|---------|---------------|-----------|
{For each published event}
| `{EventName}` | {From Unit of Work} | {Data elements} | {List subscribing contexts} |

## Domain Events Subscribed
{List External Subscriptions from Policies}

| Event Name | Source Context | Handler | Triggered Command |
|------------|----------------|---------|-------------------|
{For each external subscription}
| `{EventName}` | {Source context} | {Handler name} | `{Command}` |

## Integration Points
- **Upstream Dependencies**: {Contexts this domain subscribes to}
- **Downstream Consumers**: {Contexts that subscribe to this domain's events}

## Event Flow
```mermaid
graph LR
    {Generate event flow diagram from relationships}
```

---
*Generated from DDD specification - Domain relationships*
```

##### 4. Generate docs/business/{domain}-management.md
Populate from DDD business concepts:

**Template**:
```markdown
# {ContextName} Management - Business Requirements

## Overview
{Context description}

This domain is responsible for:
{List Units of Work as business capabilities}

## Domain Terminology (Ubiquitous Language)

Define the business terms used within this bounded context:

| Term | Definition |
|------|------------|
{Extract entity names from Commands/Events}
| {EntityName} | {Generated from context} |
| {CommandName} | {Generated from Command description} |

## Business Capabilities

{For each Unit of Work}
### {Unit of Work Name}
**Purpose**: {Logic details from DDD spec}

**Trigger**: {From Policy if exists, or API call}

**Process**:
1. Receive `{Command}` command {with parameters}
2. Execute business logic: {Logic Details}
3. Publish `{Event}` event {with data elements}

**Outputs**:
- **Event**: `{EventName}` (Published: {Yes/No})
- **Status**: Success/Failure

**Business Rules**:
- Minimal validation (required fields)
- {Any invariants listed in DDD spec}

---

## Use Cases

{For each Unit of Work}
### UC-{#}: {Unit of Work Name}
**Actor**: {Persona if exists, or "System"}  
**Goal**: Successfully execute {Unit of Work name}  
**Preconditions**:
- {Parameters required for Command}

**Main Flow**:
1. {Actor} triggers `{Command}` {via API or message}
2. System validates input parameters
3. System executes {Unit of Work} logic
4. System publishes `{Event}` event
5. {Downstream consumers} receive notification

**Postconditions**:
- `{Event}` published to event bus
- {Entity} state updated

**Alternative Flows**:
- **Validation Error**: Return 400 Bad Request with validation messages
- **Business Rule Violation**: Return 422 Unprocessable Entity

---

## Validation Rules
{Minimal validation from DDD spec}
- Required fields: {List command parameters}
- Format validation: {Data types from parameters}

## Integration Requirements

### Events Published (Outgoing)
{For each Published event}
- **`{EventName}`**: Published when {trigger}
  - **Consumers**: {List contexts with external subscriptions to this event}
  - **Data**: {List data elements}

### Events Subscribed (Incoming)
{For each External Subscription}
- **`{EventName}`**: From {Source Context}
  - **Handler**: `{HandlerName}`
  - **Action**: Triggers `{Command}` command
  - **Response**: {Response message from Policy}

---
*Document generated from DDD specification*
```

##### 5. Generate docs/technical/{domain}-technical-spec.md
Populate from DDD technical elements:

**Template**:
```markdown
# {ContextName} - Technical Specification

## Architecture

### Layer Structure
```
{ContextName}/
├── src/
│   ├── Api/              # HTTP endpoints (Port: TBD) {only if has Views}
│   ├── Domain/           # Business logic, contracts, managers
│   ├── Infrastructure/   # Cosmos DB, NServiceBus config
│   └── Endpoint.In/      # Message handlers {only if has External Subscriptions}
└── test/
    ├── Unit.Tests/       # Domain & manager tests
    └── Integration.Tests/ # Playwright API tests
```

### Technology Stack
- **.NET**: 10.0
- **NServiceBus**: 9.2.6
- **Cosmos DB**: Single-partition strategy
- **Azure Service Bus**: Message transport
- **xUnit**: Unit testing
- **Playwright**: Integration testing

---

## API Endpoints

{If has Views}
### Port Assignment
- **API**: `http://localhost:TBD` (will be assigned during implementation)
- **Endpoint.In**: Background NServiceBus host

{For each View}
### {View name} Controller

{For each Command triggered by View}
#### POST /api/{entity}/{commandAction}
**Purpose**: {Command name} - {Logic details}  
**Command**: `{CommandName}`

**Request**:
```json
{
  {Generate from Command parameters}
  "{paramName}": "{type}"
}
```

**Response**:
```json
{
  "success": true,
  "eventPublished": "{EventName}",
  {Include event data elements}
}
```

**Events Published**:
- `{EventName}` ({Event data elements})

**Error Responses**:
- `400 Bad Request`: Invalid input parameters
- `422 Unprocessable Entity`: Business rule violations
- `500 Internal Server Error`: Unexpected error

{Else}
**This domain is message-driven only** - no public HTTP API.
All operations triggered via subscribed events.

{End if}

---

## Data Model

### Cosmos DB Container
- **Container Name**: `{lowercase domain name}`
- **Partition Key**: `/{correlationId}` (e.g., `/orderId`)
- **Documents**:
  - {Entity}Document

### {Entity}Document
{Generate from Command/Event parameters}

```csharp
public class {Entity}Document
{
    [JsonPropertyName("id")]
    public string Id { get; set; }  // GUID
    
    [JsonPropertyName("{correlationIdField}")]
    public string {CorrelationId} { get; set; }  // Partition key (e.g., OrderId)
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "{Entity}";
    
    {Generate from Event data elements}
    [JsonPropertyName("{fieldName}")]
    public {FieldType} {FieldName} { get; set; }
    
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
    
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
    
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
```

---

## Message Contracts

### Commands (Internal)
*Commands this domain processes*

{For each Command}
#### {CommandName}
```csharp
namespace RiskInsure.{ContextName}.Domain.Contracts.Commands;

public record {CommandName}(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    {Generate from Command parameters}
    {ParamName} {ParamType},
    string IdempotencyKey
);
```

### Events Published (Public Contracts)
*Events published to other domains - place in PublicContracts project*

{For each Event with Published: Yes}
#### {EventName}
```csharp
namespace RiskInsure.PublicContracts.Events;

public record {EventName}(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    {Generate from Event data elements}
    {FieldName} {FieldType},
    string IdempotencyKey
);
```

### Events Subscribed
*Events this domain listens to from other domains*

{For each External Subscription}
- **`{EventName}`**: From `{SourceContext}` domain
  - Namespace: `RiskInsure.PublicContracts.Events.{EventName}`

---

## Domain Logic

### Managers

#### {Entity}Manager
**Responsibilities**:
{For each Unit of Work}
- Execute `{Unit of Work name}` business logic

**Methods**:
```csharp
{For each Unit of Work}
/// <summary>
/// {Logic details}
/// </summary>
Task<{Entity}> {UnitOfWorkName}Async({CommandName} command);
```

**Business Logic**:
{For each Unit of Work}
- **`{UnitOfWorkName}Async`**:
  1. Validate command parameters
  2. {Logic details from DDD spec}
  3. Create/update {Entity}Document
  4. Return result for event publishing

---

## Message Handlers

{If has External Subscriptions}
### Handlers in Endpoint.In

{For each Policy with External Subscription}
#### {EventName}Handler
**Message**: `{EventName}` from `{SourceContext}` domain  
**Purpose**: {Policy name}  
**Policy**: {Policy Meta - Handler name}

**Processing Logic**:
1. Receive `{EventName}` event
2. Log: {Response message from Policy}
3. Call `{Manager}.{CommandName}Async()`
4. Publish `{ResultingEvent}` event

**Handler Implementation**:
```csharp
public class {EventName}Handler : IHandleMessages<{EventName}>
{
    private readonly {Entity}Manager _manager;
    private readonly ILogger<{EventName}Handler> _logger;

    public async Task Handle({EventName} message, IMessageHandlerContext context)
    {
        _logger.LogInformation(
            "{Response message with parameters}",
            message.{CorrelationId});

        // Call domain manager
        var result = await _manager.{CommandMethodName}Async(
            new {CommandName}(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                {Map from event parameters to command},
                IdempotencyKey: $"{EventName}-{message.{CorrelationId}}"
            ));

        // Publish resulting event
        await context.Publish(new {ResultingEvent}(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            {Map from result to event parameters},
            IdempotencyKey: result.IdempotencyKey
        ));
    }
}
```

{Else}
**This domain does not subscribe to external events** - no message handlers required.
{End if}

---

## Validation Rules
**Minimal validation approach**:
- Required fields: All command parameters must be present
- Format validation: {Data types from parameters}
- Business rules: {Any invariants from DDD spec}

---

## Error Handling
- **Validation errors**: Return 400 Bad Request
- **Business rule violations**: Return 422 Unprocessable Entity
- **Idempotency**: Check for duplicate {correlationId} before processing
- **Retry logic**: NServiceBus default retry policies

---

## Testing Strategy

### Unit Tests
- Manager business logic (minimal coverage for simple logic)
- Validation logic
- Command/Event mapping

### Integration Tests
{If has API}
- API endpoint testing (Playwright)
- Command → Event flow
- Error scenarios
{Else}
- Message handler testing
- Event → Command → Event flow
- Idempotency verification
{End if}

---

## Event Flow Diagram

```mermaid
graph TD
    {Generate from DDD relationships}
    {If has API}
    API[POST /api/{entity}] --> CMD[{CommandName}]
    {Else}
    EXT[{External Event}] --> HANDLER[{EventName}Handler]
    HANDLER --> CMD[{CommandName}]
    {End if}
    CMD --> MGR[{Entity}Manager]
    MGR --> EVT[Publish {EventName}]
    EVT --> BUS[Event Bus / PublicContracts]
    {For each downstream subscriber}
    BUS --> SUB{#}[{SubscriberContext}]
    {End for}
```

---

*Generated from DDD specification*
```

##### 6. Update Solution Tracking
Log domains created and relationships:
```
Created domains:
✅ NsbSales (services/nsbsales/)
   - API: POST /api/orders
   - Publishes: OrderPlaced
   - Subscribes: (none - entry point)

✅ NsbBilling (services/nsbbilling/)
   - API: (none - message-driven)
   - Publishes: OrderBilled
   - Subscribes: Sales.OrderPlaced

✅ NsbShipping (services/nsbshipping/)
   - API: (none - message-driven)
   - Publishes: InventoryReserved, OrderShipped
   - Subscribes: Sales.OrderPlaced, Billing.OrderBilled

Event Flow:
Sales.OrderPlaced 
  → Billing.OrderPlacedHandler → Billing.OrderBilled
  → Shipping.OrderPlacedHandler → Shipping.InventoryReserved

Billing.OrderBilled
  → Shipping.OrderBilledHandler → Shipping.OrderShipped
```

---

## Execution Steps

### Step 1: Discover & Parse DDD Files
```
1. Scan services/.rawservice/ for *_Systems_single_context_final.md
2. For each file:
   a. Extract Context name from filename
   b. Parse DDD structure (Contexts, Modules, Commands, Events, Policies)
   c. Build domain model with relationships
   d. Identify event flow and dependencies
```

### Step 2: Generate Domain Folder Structure
```
For each parsed context:
1. Normalize name: Sales → nsbsales
2. Create: services/{domain}/
3. Create: services/{domain}/docs/overview.md
4. Create: services/{domain}/docs/business/{domain}-management.md
5. Create: services/{domain}/docs/technical/{domain}-technical-spec.md
6. Create: services/{domain}/README.md
```

### Step 3: Populate Documentation from DDD
```
1. Map Commands → API endpoints (if has Views) or internal commands
2. Map Events (Published: Yes) → PublicContracts events
3. Map External Subscriptions → Message handlers
4. Map Units of Work → Manager methods
5. Generate business requirements with use cases
6. Generate technical specs with contracts and handlers
```

### Step 4: Verification
- ✅ All directories created
- ✅ All documentation files exist
- ✅ Event flows documented correctly
- ✅ Handler mappings clear
- ✅ API endpoints defined (if applicable)
- ✅ Integration points documented

### Step 5: Report Results with Event Flow
```
Created domains:
✅ NsbSales (services/nsbsales/)
   - Source: services/.rawservice/Sales_Systems_single_context_final.md
   - Type: Entry point (has API)
   - Commands: PlaceOrder
   - Events Published: OrderPlaced
   - Events Subscribed: (none)

✅ NsbBilling (services/nsbbilling/)
   - Source: services/.rawservice/Billing_Systems_single_context_final.md
   - Type: Message-driven
   - Commands: BillOrder
   - Events Published: OrderBilled
   - Events Subscribed: Sales.OrderPlaced

✅ NsbShipping (services/nsbshipping/)
   - Source: services/.rawservice/Shipping_Systems_single_context_final.md
   - Type: Message-driven
   - Commands: ReserveInventory, ShipOrder
   - Events Published: InventoryReserved, OrderShipped
   - Events Subscribed: Sales.OrderPlaced, Billing.OrderBilled

📊 Event Flow:
  Sales API
    ↓ PlaceOrder
    ↓ OrderPlaced (published)
    ├──→ Billing.OrderPlacedHandler
    │      ↓ BillOrder
    │      ↓ OrderBilled (published)
    │      └──→ Shipping.OrderBilledHandler
    │             ↓ ShipOrder
    │             └──→ OrderShipped (published)
    │
    └──→ Shipping.OrderPlacedHandler
           ↓ ReserveInventory
           └──→ InventoryReserved (published)

Next Steps:
1. Review generated documentation for accuracy
2. Use domain-builder agent to generate implementation:
   @workspace Build the NsbSales domain using domain-builder agent
   @workspace Build the NsbBilling domain using domain-builder agent
   @workspace Build the NsbShipping domain using domain-builder agent
```

---

## DDD Parsing Rules

### Identify Domain Entry Points
```
IF domain has Views:
  → Create API layer with controllers
  → Commands triggered via HTTP POST
ELSE:
  → Message-driven only
  → Commands triggered via handlers
```

### Map Commands
```
Command in DDD → C# record in Domain/Contracts/Commands/
- Name: {CommandName}
- Parameters: From DDD "Parameters" section
- Namespace: RiskInsure.{ContextName}.Domain.Contracts.Commands
```

### Map Events
```
IF Event Meta "Published: Yes":
  → PublicContracts/Events/{EventName}.cs
  → Namespace: RiskInsure.PublicContracts.Events
ELSE:
  → Domain/Contracts/Events/{EventName}.cs
  → Namespace: RiskInsure.{ContextName}.Domain.Contracts.Events

Data elements → C# record properties
```

### Map External Subscriptions (Handlers)
```
Policy with "External Subscriptions":
  → Message handler in Endpoint.In/Handlers/
  → Handler name: {EventName}Handler.cs
  → Implements: IHandleMessages<{EventName}>
  → Calls: {CommandName} from policy
  → Logs: Response message from policy
```

### Map Units of Work (Managers)
```
Unit of Work → Manager method
- Manager: {Entity}Manager (one per domain)
- Method name: {UnitOfWorkName}Async
- Logic: From "Logic Details" (usually "Intuitive" = minimal logic)
- Returns: Entity for event publishing
```

### Extract Entity Names
```
From Commands/Events, extract primary entity:
- PlaceOrder, OrderPlaced → Entity: Order
- BillOrder, OrderBilled → Entity: Billing or BillingRecord
- ReserveInventory → Entity: Inventory
- ShipOrder, OrderShipped → Entity: Shipment

Use first event name as guide: {Entity}{Action}
```

### Determine Partition Key
```
Look for common GUID parameter across all Commands/Events:
- OrderID appears in all? → Partition key: /orderId
- CustomerId appears? → Partition key: /customerId

If multiple contexts share an ID, use it for correlation
```

---

## Validation Checklist

Before completing, verify:

### DDD Parsing
- [ ] All contexts extracted from .rawservice/ files
- [ ] Commands identified with parameters
- [ ] Events identified with Published flag
- [ ] External Subscriptions mapped to handlers
- [ ] Event flow relationships documented

### Documentation Structure
- [ ] Directory structure matches RiskInsure pattern
- [ ] README.md has correct context name
- [ ] All documentation files exist
- [ ] Naming conventions followed (lowercase domain folders)

### Content Quality
- [ ] Business requirements reflect DDD use cases
- [ ] Technical specs include all commands and events
- [ ] Handler mappings are correct
- [ ] API endpoints defined only for domains with Views
- [ ] Event flow diagram is accurate

### Integration Mapping
- [ ] Published events listed in PublicContracts
- [ ] External subscriptions documented as handlers
- [ ] Upstream/downstream dependencies clear
- [ ] Correlation IDs identified

---

## Example: Sales Domain Parsing

### Input DDD Spec (excerpt):
```markdown
## **Context: Sales**

**Module: Ordering**
  **View: Sales**
  **Policy: MustPlaceOrder**
    - Handler: OnPlaceOrder
  
**Unit of Work: PlacingOrder**
  **Command: PlaceOrder**
    - Parameters: OrderID={GUID}
  **Event: OrderPlaced**
    - Published: Yes
    - Data Elements: OrderID={GUID}

**Policy Relationships:**
  - External Subscriptions: (none)
```

### Output Documentation:

**Domain Model**:
```json
{
  "domainName": "nsbsales",
  "contextName": "Sales",
  "hasApi": true,
  "entities": ["Order"],
  "correlationId": "OrderID",
  "commands": [
    {"name": "PlaceOrder", "params": ["OrderID: Guid"]}
  ],
  "events": [
    {"name": "OrderPlaced", "published": true, "data": ["OrderID: Guid"]}
  ],
  "handlers": [],
  "dependencies": []
}
```

**Generated API** (technical spec):
```markdown
#### POST /api/orders
**Purpose**: Place a new order
**Command**: `PlaceOrder`

**Request**:
{
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}

**Response**:
{
  "success": true,
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventPublished": "OrderPlaced"
}
```

**Generated Event** (technical spec):
```csharp
// PublicContracts/Events/OrderPlaced.cs
namespace RiskInsure.PublicContracts.Events;

public record OrderPlaced(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid OrderId,
    string IdempotencyKey
);
```

---

## Example: Billing Domain Parsing

### Input DDD Spec (excerpt):
```markdown
## **Context: Billing**

**Module: Billing**
  **View: Billing** (no user interaction)
  **Policy: MustBillOnOrderPlaced**
    - Handler: OnOrderPlaced
  
**Unit of Work: BillingOrder**
  **Command: BillOrder**
    - Parameters: OrderID={GUID}
  **Event: OrderBilled**
    - Published: Yes
    - Data Elements: OrderID={GUID}

**Policy Relationships:**
  - External Subscriptions: Sales.OrderPlaced
  - Response Messages: "Received OrderPlaced, OrderId = {OrderID} - Charging credit card..."
```

### Output Documentation:

**Domain Model**:
```json
{
  "domainName": "nsbbilling",
  "contextName": "Billing",
  "hasApi": false,
  "entities": ["Billing"],
  "correlationId": "OrderID",
  "commands": [
    {"name": "BillOrder", "params": ["OrderID: Guid"]}
  ],
  "events": [
    {"name": "OrderBilled", "published": true, "data": ["OrderID: Guid"]}
  ],
  "handlers": [
    {
      "event": "OrderPlaced",
      "source": "Sales",
      "command": "BillOrder",
      "message": "Received OrderPlaced, OrderId = {OrderID} - Charging credit card..."
    }
  ],
  "dependencies": ["Sales"]
}
```

**Generated Handler** (technical spec):
```csharp
// Endpoint.In/Handlers/OrderPlacedHandler.cs
public class OrderPlacedHandler : IHandleMessages<OrderPlaced>
{
    private readonly BillingManager _manager;
    
    public async Task Handle(OrderPlaced message, IMessageHandlerContext context)
    {
        _logger.LogInformation(
            "Received OrderPlaced, OrderId = {OrderID} - Charging credit card...",
            message.OrderId);

        var result = await _manager.BillOrderAsync(
            new BillOrder(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                OrderId: message.OrderId,
                IdempotencyKey: $"BillOrder-{message.OrderId}"
            ));

        await context.Publish(new OrderBilled(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            OrderId: result.OrderId,
            IdempotencyKey: result.IdempotencyKey
        ));
    }
}
```

---

## Usage Example

```
@workspace Use domain-scaffolder agent to discover and process DDD specifications.

Scan services/.rawservice/ for DDD specification files and create complete documentation structure for all discovered contexts. Parse the DDD format and generate business and technical documentation following RiskInsure architecture patterns.
```

**Or specify explicitly:**
```
@workspace Process these DDD specifications: Sales_Systems_single_context_final.md, Billing_Systems_single_context_final.md, Shipping_Systems_single_context_final.md

Create domains: NsbSales, NsbBilling, NsbShipping with complete documentation from DDD specs.
```

---

## Constitutional Compliance

This agent follows:
- **Principle I**: Clear bounded contexts (extracts from DDD Context definitions)
- **Principle II**: Message-based integration (maps External Subscriptions to handlers)
- **Principle III**: Single-partition Cosmos DB (documents share correlation ID)
- **Principle V**: Thin handlers (documented in technical specs)
- **Principle IX**: Standards compliance (follows RiskInsure naming and structure)

---

## Key Differentiators from Simple Scaffolding

### DDD-Aware Capabilities
1. **Automatic Discovery**: Scans `.rawservice/` folder for DDD specs
2. **Relationship Mapping**: Understands event flows between contexts
3. **Handler Generation**: Maps External Subscriptions to message handlers
4. **API Detection**: Only creates APIs for domains with Views
5. **Event Classification**: Distinguishes public vs internal events
6. **Correlation Tracking**: Identifies shared IDs across contexts

### Generic Domain Processing
- Works with **any** DDD specification following the template format
- Extracts domain relationships automatically
- Generates complete event flow documentation
- Maps DDD patterns to RiskInsure architecture patterns
- No hardcoded domain knowledge required

### Translation Strategy
**DDD Structure** (Event Storming + DDD Strategic Design):
- Contexts, Modules, Policies, Units of Work, Commands, Events
- External Subscriptions, Views, Personas

**↓ Translates to ↓**

**RiskInsure Architecture** (Event-Driven Microservices):
- Services, Managers, API Controllers, Message Handlers
- Internal Commands, Public Events, Message Contracts

---

## Related Agents
- **domain-builder**: Use after documentation is complete to generate full implementation
- **documentation-sync**: Keep documentation in sync with implementation
- **e2e-contract-verifier**: Verify event contracts match across domains

---

## Troubleshooting

### Issue: DDD file not found
**Solution**: Ensure files are in `services/.rawservice/` with naming: `{Context}_Systems_single_context_final.md`

### Issue: Events not mapping correctly
**Check**: 
- Event has `*Event Meta*` section with `Published: Yes`
- External Subscriptions use format: `{ContextName}.{EventName}`

### Issue: No API generated for domain
**Reason**: Domain has no Views in DDD spec (message-driven design)
**Expected**: Billing and Shipping are correctly message-driven only

### Issue: Handler mapping unclear
**Check**:
- Policy has `*External Subscriptions*` section
- Policy has `*Command*` section (triggered by handler)
- Response Messages show handler log output

### Issue: Entity names generic
**Solution**: DDD specs may use generic names (Order, Billing, Shipment)
**Action**: Review and refine in business requirements documentation

---

## Future Enhancements

1. **Multi-file DDD Support**: Handle DDD specs split across multiple files
2. **Visualization**: Generate Mermaid diagrams from event flows
3. **Validation**: Verify event contracts match across publishers and subscribers
4. **Port Assignment**: Suggest available ports during scaffolding
5. **Migration Support**: Convert existing domains to DDD documentation format
