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

## Integration Points Template

Document integration points for this domain:

### Messages Published
- `EventCreatedEvent` - Published when a new event is created
- `EventStatusChangedEvent` - Published when event status changes
- `EventExpiredEvent` - Published when event expires
- (Add more as implemented)

### Messages Consumed
- (None currently - add as integration is implemented)

### Platform APIs Used
- (Document any platform composition APIs used)

### External Systems
- (Document any external system integrations)

## Related Files
- See [messaging-patterns.md](messaging-patterns.md) for message design
- See [domain-events.md](domain-events.md) for event patterns
- See [data-patterns.md](data-patterns.md) for data isolation
- See [distributed-systems-fallacies.md](distributed-systems-fallacies.md) for integration challenges
