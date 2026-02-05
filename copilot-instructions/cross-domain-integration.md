# Cross-Domain Integration

## Overview

Integration between bounded contexts in this system is **primarily message-based**, following event-driven architecture principles. This approach maintains loose coupling between domains while enabling them to collaborate effectively.

## Integration Approaches

### 1. Message-Based Integration (Primary)

Messages are the primary integration mechanism between bounded contexts.

#### Integration Events

Events published by one bounded context can be subscribed to by other contexts:

```csharp
// Published by EventManagement domain
public class EventCreatedEvent
{
    public Guid EventId { get; set; }
    public string EventName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

// Consumed by other domains (e.g., Ticketing, Notifications)
public class EventCreatedEventHandler : IHandleMessages<EventCreatedEvent>
{
    public async Task Handle(EventCreatedEvent message, IMessageHandlerContext context)
    {
        // React to event in this domain's context
    }
}
```

#### Commands Across Domains

Commands can trigger actions in other domains, but should be used sparingly:

```csharp
// Sent to another domain
public class ReserveTicketsCommand
{
    public Guid EventId { get; set; }
    public int Quantity { get; set; }
}
```

**Guidelines:**
- Prefer events over commands for cross-domain communication
- Commands create coupling - use only when coordination is required
- Events represent facts - domains react independently
- Use correlation IDs to track related operations across domains

### 2. Platform Composition Layer

Platform components compose data and functionality from multiple domains without violating bounded context boundaries.

#### API Composition

The platform may include a composition API that aggregates data from multiple domain APIs:

```
┌─────────────────────────────────────┐
│     Platform Composition API        │
│  (Aggregates data from domains)     │
└─────────────┬───────────────────────┘
              │
    ┌─────────┼─────────────┐
    │         │             │
    ▼         ▼             ▼
┌─────┐   ┌─────┐      ┌────────┐
│Event│   │Ticket│     │Customer│
│ API │   │ API  │     │  API   │
└─────┘   └─────┘      └────────┘
```

**Characteristics:**
- Platform layer has no domain logic
- Each domain API remains independent
- Composition happens at HTTP/messaging boundaries
- No direct database access across domains

#### UI Composition

Frontend applications may compose UI from multiple domain components:

```typescript
// Platform UI composes domain components
<EventDetailsPage>
  <EventInformation from={eventService} />
  <TicketSelection from={ticketService} />
  <CustomerProfile from={customerService} />
</EventDetailsPage>
```

**Principles:**
- Each domain owns its UI components
- Platform orchestrates layout and navigation
- No shared state between domain components
- Use events for inter-component communication

### 3. Consumer-Driven Contracts

Domains publish contracts (schemas) that consumers depend on, ensuring backward compatibility.

#### Contract Publishing

Each domain publishes its message contracts:

```csharp
// InternalContracts project
namespace AcmeTickets.EventManagement.Contracts
{
    public class EventCreatedEvent
    {
        public Guid EventId { get; set; }
        public string EventName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        // Contract version: 1.0
    }
}
```

#### Contract Testing

Consumers test against published contracts:

```csharp
// Consumer tests in another domain
public class EventCreatedEventContractTests
{
    [Fact]
    public void EventCreatedEvent_HasExpectedProperties()
    {
        var @event = new EventCreatedEvent
        {
            EventId = Guid.NewGuid(),
            EventName = "Test Event",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1)
        };
        
        // Verify contract properties exist
        Assert.NotEqual(Guid.Empty, @event.EventId);
        Assert.NotNull(@event.EventName);
    }
}
```

#### Contract Versioning

When contracts evolve, maintain backward compatibility:

```csharp
// Version 2 adds optional property
public class EventCreatedEvent
{
    public Guid EventId { get; set; }
    public string EventName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    // New in v2 - optional for backward compatibility
    public string? VenueId { get; set; }
    public string? Description { get; set; }
}
```

**Versioning Strategies:**
- Add new optional properties (non-breaking)
- Never remove properties (breaking)
- Use message versioning for breaking changes
- Maintain old message types alongside new ones
- Retire old versions only after all consumers upgrade

### 4. Anti-Corruption Layer

When integrating with external systems or legacy systems, use an anti-corruption layer to translate between models.

```csharp
// Anti-corruption layer translates external model to domain model
public class LegacyEventAdapter
{
    public Event TranslateToEvent(LegacyEventDto legacyEvent)
    {
        return Event.Create(
            name: legacyEvent.EventTitle,
            startDate: DateTime.Parse(legacyEvent.StartDateString),
            endDate: DateTime.Parse(legacyEvent.EndDateString)
        );
    }
    
    public LegacyEventDto TranslateFromEvent(Event domainEvent)
    {
        return new LegacyEventDto
        {
            EventTitle = domainEvent.Name,
            StartDateString = domainEvent.StartDate.ToString("yyyy-MM-dd"),
            EndDateString = domainEvent.EndDate.ToString("yyyy-MM-dd")
        };
    }
}
```

**Purpose:**
- Protect domain model from external influences
- Translate between different ubiquitous languages
- Isolate domain from external system changes
- Maintain clean domain boundaries

## Integration Patterns

### Choreography vs Orchestration

#### Choreography (Preferred)
Domains react to events independently without central coordination:

```
EventManagement              Ticketing              Notifications
    │                            │                       │
    │──EventCreatedEvent────────>│                       │
    │                            │                       │
    │                            │──Processes event      │
    │                            │                       │
    │──EventCreatedEvent─────────────────────────────────>│
    │                            │                       │
    │                            │                       │──Sends notification
```

**Advantages:**
- Loose coupling
- Independent scaling
- Easier to add new consumers
- No single point of failure

#### Orchestration (When Needed)
Central coordinator manages multi-step process:

```csharp
// Saga orchestrates multi-domain workflow
public class CreateEventWorkflowSaga : Saga<CreateEventWorkflowData>
{
    public async Task Handle(StartCreateEventWorkflow message, IMessageHandlerContext context)
    {
        // Step 1: Create event
        await context.Send(new CreateEventCommand { ... });
    }
    
    public async Task Handle(EventCreatedEvent message, IMessageHandlerContext context)
    {
        Data.EventId = message.EventId;
        
        // Step 2: Create ticket allocations
        await context.Send(new CreateTicketAllocationCommand { ... });
    }
    
    public async Task Handle(TicketAllocationCreatedEvent message, IMessageHandlerContext context)
    {
        // Step 3: Send notifications
        await context.Send(new SendEventCreatedNotificationCommand { ... });
        
        MarkAsComplete();
    }
}
```

**Use When:**
- Transaction-like behavior needed across domains
- Complex compensation logic required
- Clear ownership of process
- Strict ordering requirements

### Read Model Replication

Domains may replicate read-only data from other domains for query purposes:

```csharp
// Ticketing domain maintains read model of events
public class EventReadModel
{
    public Guid EventId { get; set; }
    public string EventName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; }
}

// Updated when EventManagement publishes events
public class EventReadModelUpdater : IHandleMessages<EventCreatedEvent>,
                                      IHandleMessages<EventStatusChangedEvent>
{
    private readonly IEventReadModelRepository _repository;
    
    public async Task Handle(EventCreatedEvent message, IMessageHandlerContext context)
    {
        await _repository.UpsertAsync(new EventReadModel
        {
            EventId = message.EventId,
            EventName = message.EventName,
            StartDate = message.StartDate,
            EndDate = message.EndDate,
            Status = "Active"
        });
    }
    
    public async Task Handle(EventStatusChangedEvent message, IMessageHandlerContext context)
    {
        var readModel = await _repository.GetAsync(message.EventId);
        readModel.Status = message.NewStatus;
        await _repository.UpdateAsync(readModel);
    }
}
```

**Guidelines:**
- Only replicate what's needed for queries
- Source domain owns the data
- Keep replicated data simple
- Handle eventual consistency
- Use for performance, not domain logic

### Reference by ID

When domains need to reference entities from other domains, use IDs not entire objects:

```csharp
// Good: Reference by ID
public class Ticket
{
    public Guid TicketId { get; set; }
    public Guid EventId { get; set; }  // Reference to Event in EventManagement domain
    public decimal Price { get; set; }
}

// Bad: Embedded entity from another domain
public class Ticket
{
    public Guid TicketId { get; set; }
    public Event Event { get; set; }  // Don't embed Event entity
    public decimal Price { get; set; }
}
```

## Data Consistency

### Eventual Consistency

Cross-domain operations are eventually consistent:

```csharp
// 1. Event created
await _eventRepository.SaveAsync(newEvent);
await context.Publish(new EventCreatedEvent { EventId = newEvent.Id });

// 2. Message is delivered (eventually)
// 3. Other domains process event (eventually)
// 4. System reaches consistent state (eventually)
```

**Handling Eventual Consistency:**
- Design UI to handle delays
- Show "Processing..." states
- Use polling or SignalR for updates
- Provide user feedback
- Implement idempotent operations

### Distributed Transactions (Avoid)

**Don't use distributed transactions** across bounded contexts:

```csharp
// ❌ DON'T DO THIS
using (var scope = new TransactionScope())
{
    await _eventRepository.SaveAsync(newEvent);
    await _ticketRepository.CreateTickets(newEvent.Id);
    scope.Complete();
}
```

Instead, use saga pattern with compensation:

```csharp
// ✅ DO THIS - Saga with compensation
public class CreateEventWithTicketsSaga : Saga<CreateEventWithTicketsData>
{
    public async Task Handle(CreateEventWithTickets command, IMessageHandlerContext context)
    {
        await context.Send(new CreateEventCommand { ... });
    }
    
    public async Task Handle(EventCreatedEvent message, IMessageHandlerContext context)
    {
        Data.EventId = message.EventId;
        await context.Send(new CreateTicketsCommand { EventId = Data.EventId });
    }
    
    public async Task Handle(CreateTicketsFailedEvent message, IMessageHandlerContext context)
    {
        // Compensate: Delete event
        await context.Send(new DeleteEventCommand { EventId = Data.EventId });
    }
}
```

## Integration Testing

Test cross-domain integration with contract tests and integration tests:

```csharp
// Contract test
public class EventManagementContractTests
{
    [Fact]
    public async Task WhenEventCreated_PublishesEventCreatedEvent()
    {
        // Arrange
        var handler = new CreateEventCommandHandler(...);
        var capturedEvents = new List<object>();
        var context = new MockMessageHandlerContext(capturedEvents);
        
        // Act
        await handler.Handle(new CreateEventCommand { ... }, context);
        
        // Assert
        var publishedEvent = capturedEvents.OfType<EventCreatedEvent>().Single();
        Assert.NotEqual(Guid.Empty, publishedEvent.EventId);
        Assert.NotNull(publishedEvent.EventName);
    }
}

// Integration test
public class CrossDomainIntegrationTests
{
    [Fact]
    public async Task EventCreatedEvent_IsConsumedByTicketingDomain()
    {
        // Test actual message flow between domains
        // This would require both domains running
    }
}
```

## Best Practices

### DO
- ✅ Use message-based integration as primary mechanism
- ✅ Publish events when significant things happen
- ✅ Design for eventual consistency
- ✅ Use correlation IDs for tracing
- ✅ Version contracts carefully
- ✅ Test contracts independently
- ✅ Use anti-corruption layer for external systems
- ✅ Reference other domains by ID
- ✅ Replicate read models when needed for queries
- ✅ Document integration points clearly

### DON'T
- ❌ Share databases between domains
- ❌ Make synchronous calls between domains
- ❌ Use distributed transactions
- ❌ Embed entities from other domains
- ❌ Create tight coupling through shared logic
- ❌ Break contracts without versioning
- ❌ Access another domain's database directly
- ❌ Use RPC-style communication for everything
- ❌ Assume immediate consistency

## Technical Specification Requirements

### Purpose

When creating technical specifications (`docs/technical/highlevel-tech.md`), you MUST document integration points with precision to enable:

1. **Code Generation**: Specifications should be detailed enough for AI tools to generate handler code
2. **Contract Verification**: Enable verification that publisher-subscriber agreements are valid
3. **Cross-Domain Coordination**: Ensure domains agree on event contracts and responsibilities

---

## Published Events Documentation

### What to Document

**DO**: Document all public events your domain publishes
**DON'T**: Document where events are published to (you don't know who's listening)

### Required Information for Each Published Event

Each event in your technical specification MUST include:

1. **Event Name** - Exact C# record name (e.g., `FundsSettled`, `BillingAccountCreated`)
2. **Purpose** - When and why this event is published
3. **Trigger** - What action/state change causes this event
4. **Payload Summary** - Key fields in the event (with types)
5. **Contract Location** - Where the event contract is defined (`platform/RiskInsure.PublicContracts/Events/` or `Domain/Contracts/Events/`)

### Format: Published Events Table

```markdown
## Events Published

| Event Name | Purpose | Trigger | Payload Summary | Contract Location |
|------------|---------|---------|-----------------|-------------------|
| `FundsSettled` | Notifies that customer payment was successfully settled | Payment authorization and settlement completed | CustomerId (Guid), Amount (decimal), PaymentMethodId (Guid), TransactionId (string), SettledUtc (DateTimeOffset) | `platform/RiskInsure.PublicContracts/Events/FundsSettled.cs` |
| `PaymentMethodAdded` | Notifies that new payment method was validated and stored | Payment method creation and validation succeeded | CustomerId (Guid), PaymentMethodId (Guid), MethodType (enum), MaskedDetails (string) | `platform/RiskInsure.PublicContracts/Events/PaymentMethodAdded.cs` |
```

**Example from FundTransferMgt domain** (`services/fundstransfermgt/docs/technical/highlevel-tech.md`):

```markdown
## Events Published

This domain publishes the following public events to the platform event bus:

| Event Name | Purpose | Trigger | Payload Summary | Contract Location |
|------------|---------|---------|-----------------|-------------------|
| `FundsSettled` | Customer payment successfully authorized and settled | Fund transfer completes authorization and settlement | CustomerId, Amount, PaymentMethodId, TransactionId, SettledUtc, IdempotencyKey | `platform/RiskInsure.PublicContracts/Events/FundsSettled.cs` |
| `FundsRefunded` | Refund processed and returned to customer | Refund transaction completes | CustomerId, RefundId, OriginalTransactionId, Amount, RefundedUtc, Reason | `platform/RiskInsure.PublicContracts/Events/FundsRefunded.cs` |
| `PaymentMethodAdded` | New payment instrument validated and stored | Payment method creation succeeds validation | CustomerId, PaymentMethodId, MethodType, Status, CreatedUtc | `platform/RiskInsure.PublicContracts/Events/PaymentMethodAdded.cs` |
| `PaymentMethodRemoved` | Payment instrument deleted or invalidated | Payment method deletion requested | CustomerId, PaymentMethodId, RemovalReason, RemovedUtc | `platform/RiskInsure.PublicContracts/Events/PaymentMethodRemoved.cs` |
| `TransferAuthorizationFailed` | Payment authorization failed | Authorization attempt rejected by gateway | CustomerId, TransactionId, PaymentMethodId, Amount, FailureReason, ErrorCode | `platform/RiskInsure.PublicContracts/Events/TransferAuthorizationFailed.cs` |

**Note**: These events are published to the platform event bus. Subscribers across all domains can listen to these events. We do not control or know which domains subscribe.
```

---

## Subscribed Events Documentation

### What to Document

**DO**: Document all events your domain subscribes to/handles
**DO**: Document which domain publishes each event
**DO**: Document what action your domain takes when handling the event

### Required Information for Each Subscribed Event

Each subscribed event MUST include:

1. **Event Name** - Exact C# record name
2. **Publishing Domain** - Which bounded context publishes this event
3. **Handler Name** - The IHandleMessages<T> class that will process it
4. **Purpose** - Why your domain needs to react to this event
5. **Action Taken** - What your domain does when this event arrives
6. **Contract Location** - Where the event contract is defined

### Format: Subscribed Events Table

```markdown
## Events Subscribed To

| Event Name | Publishing Domain | Handler Name | Purpose | Action Taken | Contract Location |
|------------|------------------|--------------|---------|--------------|-------------------|
| `FundsSettled` | FundTransferMgt | `FundsSettledHandler` | Record payment when funds settle | Updates billing account: reduces outstanding balance, increases total paid, publishes `PaymentReceived` event | `platform/RiskInsure.PublicContracts/Events/FundsSettled.cs` |
| `FundsRefunded` | FundTransferMgt | `FundsRefundedHandler` | Record refund when funds returned | Updates billing account: increases outstanding balance, decreases total paid, publishes `RefundProcessed` event | `platform/RiskInsure.PublicContracts/Events/FundsRefunded.cs` |
```

**Example from Billing domain** (`services/billing/docs/technical/highlevel-tech.md`):

```markdown
## Events Subscribed To

This domain subscribes to and handles the following events from other domains:

| Event Name | Publishing Domain | Handler Name | Purpose | Action Taken | Contract Location |
|------------|------------------|--------------|---------|--------------|-------------------|
| `FundsSettled` | FundTransferMgt | `FundsSettledHandler` | Record payment when customer funds successfully settle | Retrieves billing account, applies payment, reduces outstanding balance, increases total paid, publishes `PaymentReceived` event | `platform/RiskInsure.PublicContracts/Events/FundsSettled.cs` |
| `FundsRefunded` | FundTransferMgt | `FundsRefundedHandler` | Record refund when funds returned to customer | Retrieves billing account, reverses payment, increases outstanding balance, decreases total paid, publishes `RefundProcessed` event | `platform/RiskInsure.PublicContracts/Events/FundsRefunded.cs` |
| `TransferAuthorizationFailed` | FundTransferMgt | `TransferAuthorizationFailedHandler` | Track failed payment attempts for customer service visibility | Logs failed payment attempt, may trigger notification to customer service team | `platform/RiskInsure.PublicContracts/Events/TransferAuthorizationFailed.cs` |

**Handler Implementation**: Each handler should be implemented in `src/Endpoint.In/Handlers/` and follow the thin handler pattern (validate → call manager → publish events).
```

---

## Contract Verification Rules

### Publisher Responsibilities

When documenting **published events** in your technical specification:

1. ✅ **Define the complete event contract** in the appropriate location:
   - Cross-domain (public): `platform/RiskInsure.PublicContracts/Events/`
   - Internal only: `services/{ServiceName}/src/Domain/Contracts/Events/`

2. ✅ **Document all required fields** with types and business meaning

3. ✅ **Include standard event metadata**:
   - `MessageId` (Guid)
   - `OccurredUtc` (DateTimeOffset)
   - `IdempotencyKey` (string)

4. ✅ **Specify trigger conditions** - what business action causes this event

### Subscriber Responsibilities

When documenting **subscribed events** in your technical specification:

1. ✅ **Verify the publishing domain documents this event** in their technical spec

2. ✅ **Reference the exact event contract location** - must match publisher's documentation

3. ✅ **Document the handler name** using convention: `{EventName}Handler`

4. ✅ **Describe business logic clearly** - sufficient detail for code generation

5. ✅ **Identify dependencies** - what repositories, managers, or services the handler needs

### Verification Checklist

Before finalizing technical specifications, verify:

- [ ] All published events have complete contracts defined
- [ ] All published events are documented in the Events Published table
- [ ] All subscribed events reference a publishing domain
- [ ] For each subscribed event, the publishing domain's spec documents that event
- [ ] Event names match exactly between publisher and subscriber documentation
- [ ] Contract locations match between publisher and subscriber documentation
- [ ] Handler names follow naming convention: `{EventName}Handler`
- [ ] Handler actions are detailed enough for code generation

---

## Code Generation Requirements

### Handler Template Structure

Your technical specification should provide enough detail to generate:

```csharp
namespace RiskInsure.Billing.Endpoint.In.Handlers;

/// <summary>
/// Handles FundsSettled events from FundTransferMgt domain.
/// Purpose: Record payment when customer funds successfully settle.
/// </summary>
public class FundsSettledHandler : IHandleMessages<FundsSettled>
{
    private readonly IBillingPaymentManager _paymentManager;
    private readonly ILogger<FundsSettledHandler> _logger;

    public FundsSettledHandler(
        IBillingPaymentManager paymentManager,
        ILogger<FundsSettledHandler> logger)
    {
        _paymentManager = paymentManager;
        _logger = logger;
    }

    public async Task Handle(FundsSettled message, IMessageHandlerContext context)
    {
        _logger.LogInformation(
            "Processing FundsSettled event for CustomerId={CustomerId}, Amount={Amount}, TransactionId={TransactionId}",
            message.CustomerId, message.Amount, message.TransactionId);

        // Action: Apply payment to billing account
        var dto = new RecordPaymentDto
        {
            AccountId = message.CustomerId, // Map to billing account
            Amount = message.Amount,
            ReferenceNumber = message.TransactionId,
            IdempotencyKey = message.IdempotencyKey,
            OccurredUtc = message.SettledUtc
        };

        var result = await _paymentManager.RecordPaymentAsync(dto, context.CancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "Payment recorded successfully for Account={AccountId}, Amount={Amount}",
                dto.AccountId, dto.Amount);
        }
        else
        {
            _logger.LogError(
                "Failed to record payment: {ErrorMessage} ({ErrorCode})",
                result.ErrorMessage, result.ErrorCode);
            throw new InvalidOperationException($"Payment recording failed: {result.ErrorMessage}");
        }
    }
}
```

### Required Documentation for Code Generation

To generate the above handler, your technical spec MUST document:

1. **Event contract fields** - CustomerId, Amount, TransactionId, SettledUtc, IdempotencyKey
2. **Manager interface to call** - `IBillingPaymentManager.RecordPaymentAsync()`
3. **DTO mapping** - How event fields map to manager DTO fields
4. **Error handling strategy** - Log and throw, or compensate
5. **Dependencies** - Manager, logger, repositories needed

---

## Integration Points Documentation Template

Use this template in your `docs/technical/highlevel-tech.md`:

### For Publishers

```markdown
## Events Published

This domain publishes the following public events to the platform event bus:

| Event Name | Purpose | Trigger | Payload Summary | Contract Location |
|------------|---------|---------|-----------------|-------------------|
| `EventName` | Business purpose | What causes it | Key fields with types | Path to contract file |

**Publishing Notes**:
- Events are published via `context.Publish()` in NServiceBus
- All events include standard metadata: MessageId, OccurredUtc, IdempotencyKey
- Subscribers are not controlled by this domain (pub/sub pattern)
- Events represent facts that have already occurred (past tense naming)
```

### For Subscribers

```markdown
## Events Subscribed To

This domain subscribes to and handles the following events from other domains:

| Event Name | Publishing Domain | Handler Name | Purpose | Action Taken | Contract Location |
|------------|------------------|--------------|---------|--------------|-------------------|
| `EventName` | DomainName | `EventNameHandler` | Why we care | What we do | Path to contract file |

**Handler Implementation**:
- Handlers located in: `src/Endpoint.In/Handlers/`
- Follow thin handler pattern: validate → call manager → publish events
- All handlers must be idempotent (safe to replay)
- Error handling: [describe strategy - retry, compensate, alert]

**Dependencies**:
- Managers: [list required manager interfaces]
- Repositories: [list if direct repository access needed]
- External Services: [list any external dependencies]
```

### Platform APIs Used
- (Document any platform composition APIs used)

### External Systems
- (Document any external system integrations)

---

## Example: Complete Integration Documentation

**From Billing Technical Spec** (`services/billing/docs/technical/highlevel-tech.md`):

```markdown
## Integration Points

### Events Published

This domain publishes the following public events to the platform event bus:

| Event Name | Purpose | Trigger | Payload Summary | Contract Location |
|------------|---------|---------|-----------------|-------------------|
| `BillingAccountCreated` | Notify that new billing account established | Account creation succeeds | AccountId, CustomerId, PolicyNumber, PremiumOwed, BillingCycle, EffectiveDate | `platform/RiskInsure.PublicContracts/Events/BillingAccountCreated.cs` |
| `PaymentReceived` | Notify that payment recorded against account | Payment successfully applied to account | AccountId, Amount, ReferenceNumber, TotalPaid, OutstandingBalance | `platform/RiskInsure.PublicContracts/Events/PaymentReceived.cs` |
| `AccountClosed` | Notify that billing account permanently closed | Account closure requested | AccountId, PolicyNumber, ClosureReason, FinalBalance | `platform/RiskInsure.PublicContracts/Events/AccountClosed.cs` |

### Events Subscribed To

This domain subscribes to and handles the following events from other domains:

| Event Name | Publishing Domain | Handler Name | Purpose | Action Taken | Contract Location |
|------------|------------------|--------------|---------|--------------|-------------------|
| `FundsSettled` | FundTransferMgt | `FundsSettledHandler` | Record payment when funds settle | Apply payment via `BillingPaymentManager.RecordPaymentAsync()`, reduce balance, publish `PaymentReceived` | `platform/RiskInsure.PublicContracts/Events/FundsSettled.cs` |
| `FundsRefunded` | FundTransferMgt | `FundsRefundedHandler` | Record refund when funds returned | Reverse payment via manager, increase balance, publish `RefundProcessed` | `platform/RiskInsure.PublicContracts/Events/FundsRefunded.cs` |

**Handler Dependencies**:
- `IBillingPaymentManager` - For recording payments and refunds
- `IBillingAccountRepository` - For retrieving accounts (via manager)
- `ILogger<HandlerName>` - For structured logging

**Error Handling**:
- Transient failures: NServiceBus retries automatically (configured in endpoint)
- Permanent failures: Log error, send to error queue for manual intervention
- Idempotency: Handlers check for duplicate operations using IdempotencyKey
```

## Related Files
- See [messaging-patterns.md](messaging-patterns.md) for message design
- See [domain-events.md](domain-events.md) for event patterns
- See [data-patterns.md](data-patterns.md) for data isolation
- See [distributed-systems-fallacies.md](distributed-systems-fallacies.md) for integration challenges
