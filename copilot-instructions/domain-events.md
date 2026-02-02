# Domain Events

## Overview

Domain events are a pattern for capturing and communicating significant state changes within domain entities. They enable loose coupling between components and form the foundation for event-driven architecture.

## What Are Domain Events?

### Definition
A domain event represents something meaningful that happened in the domain model. Events are facts about past occurrences.

### Characteristics
- **Past tense naming**: Describes what happened (e.g., `EventCreated`, `StatusChanged`)
- **Immutable**: Once raised, cannot be changed
- **Raised by entities**: Domain entities generate events when their state changes
- **Published by infrastructure**: Handlers convert domain events to integration events

## Domain Events vs Integration Events

### Domain Events
- **Scope**: Within the bounded context
- **Location**: Raised by domain entities, stored in entity
- **Purpose**: Track aggregate state changes
- **Visibility**: Internal to domain layer

### Integration Events
- **Scope**: Cross-bounded context
- **Location**: Published via NServiceBus
- **Purpose**: Notify other domains/services
- **Visibility**: External via message bus

### Relationship
```
Entity raises Domain Event
    ↓
Handler processes entity
    ↓
Handler publishes Integration Event
    ↓
Other domains/handlers receive Integration Event
```

## Implementing Domain Events

### Base Domain Event Class

```csharp
// Domain/Events/DomainEvent.cs
public abstract class DomainEvent
{
    public DateTime OccurredAt { get; }
    
    protected DomainEvent()
    {
        OccurredAt = DateTime.UtcNow;
    }
}
```

### Concrete Domain Events

```csharp
// Domain/Events/EventCreatedEvent.cs
public class EventCreatedEvent : DomainEvent
{
    public Guid EventId { get; }
    public string EventName { get; }
    
    public EventCreatedEvent(Guid eventId, string eventName)
    {
        EventId = eventId;
        EventName = eventName;
    }
}

// Domain/Events/EventExpiredEvent.cs
public class EventExpiredEvent : DomainEvent
{
    public Guid EventId { get; }
    
    public EventExpiredEvent(Guid eventId)
    {
        EventId = eventId;
    }
}

// Domain/Events/EventClosedEvent.cs
public class EventClosedEvent : DomainEvent
{
    public Guid EventId { get; }
    
    public EventClosedEvent(Guid eventId)
    {
        EventId = eventId;
    }
}
```

## Raising Events in Entities

### Entity with Event Collection

```csharp
// Domain/Entities/Event.cs
public class Event
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public EventStatus Status { get; set; }
    
    private readonly List<DomainEvent> _domainEvents = new();
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    // Factory method raises event
    public static Event Create(string name, DateTime startDate, DateTime endDate)
    {
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            Status = EventStatus.Active
        };
        
        evt.AddDomainEvent(new EventCreatedEvent(evt.Id, evt.Name));
        return evt;
    }
    
    // Business methods raise events
    public void Expire()
    {
        if (Status != EventStatus.Active)
            throw new InvalidOperationException("Only active events can be expired.");
            
        Status = EventStatus.Expired;
        AddDomainEvent(new EventExpiredEvent(Id));
    }
    
    public void Close()
    {
        if (Status != EventStatus.Active)
            throw new InvalidOperationException("Only active events can be closed.");
            
        Status = EventStatus.Closed;
        AddDomainEvent(new EventClosedEvent(Id));
    }
    
    private void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
    
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
```

## Publishing Domain Events

### From Message Handler

```csharp
// Infrastructure/Handlers/CreateEventCommandHandler.cs
public class CreateEventCommandHandler : IHandleMessages<CreateEventCommand>
{
    private readonly IEventRepository _repository;
    
    public async Task Handle(CreateEventCommand message, IMessageHandlerContext context)
    {
        // 1. Create entity (raises domain event internally)
        var evt = Event.Create(message.Name, message.StartDate, message.EndDate);
        
        // 2. Save entity
        await _repository.SaveAsync(evt);
        
        // 3. Publish domain events as integration events
        foreach (var domainEvent in evt.DomainEvents)
        {
            await PublishIntegrationEvent(domainEvent, context);
        }
        
        // 4. Clear events after publishing
        evt.ClearDomainEvents();
    }
    
    private async Task PublishIntegrationEvent(DomainEvent domainEvent, IMessageHandlerContext context)
    {
        switch (domainEvent)
        {
            case EventCreatedEvent e:
                await context.Publish(new EventCreatedIntegrationEvent
                {
                    EventId = e.EventId,
                    EventName = e.EventName,
                    CreatedAt = e.OccurredAt
                });
                break;
                
            case EventExpiredEvent e:
                await context.Publish(new EventExpiredIntegrationEvent
                {
                    EventId = e.EventId,
                    ExpiredAt = e.OccurredAt
                });
                break;
                
            case EventClosedEvent e:
                await context.Publish(new EventClosedIntegrationEvent
                {
                    EventId = e.EventId,
                    ClosedAt = e.OccurredAt
                });
                break;
        }
    }
}
```

### Alternative: Generic Event Dispatcher

```csharp
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<DomainEvent> events, IMessageHandlerContext context);
}

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IEnumerable<IDomainEventHandler> _handlers;
    
    public async Task DispatchAsync(IEnumerable<DomainEvent> events, IMessageHandlerContext context)
    {
        foreach (var domainEvent in events)
        {
            var eventType = domainEvent.GetType();
            var handlersForEvent = _handlers.Where(h => h.CanHandle(eventType));
            
            foreach (var handler in handlersForEvent)
            {
                await handler.HandleAsync(domainEvent, context);
            }
        }
    }
}
```

## Event Naming Conventions

### Domain Events
- **Location**: `Domain/Events/`
- **Naming**: `{Entity}{Action}Event` in past tense
- **Examples**:
  - `EventCreatedEvent`
  - `EventExpiredEvent`
  - `StatusChangedEvent`
  - `ValidationFailedEvent`

### Integration Events
- **Location**: `InternalContracts/Events/` or public contracts package
- **Naming**: Same as domain event or with `Integration` suffix for clarity
- **Examples**:
  - `EventCreatedIntegrationEvent`
  - `EventExpiredIntegrationEvent`

## When to Raise Domain Events

### Do Raise Events For:
✅ State changes (status transitions)
✅ Entity creation
✅ Significant business operations
✅ Actions that other parts of the system care about
✅ Validation successes/failures
✅ Aggregate lifecycle events

### Don't Raise Events For:
❌ Property setters (too granular)
❌ Private implementation details
❌ Temporary or calculated values
❌ Events no one subscribes to

## Benefits of Domain Events

### Loose Coupling
- Entities don't know about external systems
- Handlers can be added without changing entities
- Easy to extend functionality

### Audit Trail
- Events provide history of what happened
- Can reconstruct entity state from events (Event Sourcing)
- Natural audit log

### Side Effects
- Trigger workflows without tight coupling
- Send notifications
- Update read models
- Integrate with external systems

### Testability
- Test entity behavior by checking raised events
- Verify events without complex infrastructure
- Easy to mock event handling

## Testing Domain Events

### Unit Test Example

```csharp
[Fact]
public void Create_RaisesEventCreatedEvent()
{
    // Arrange
    var name = "Test Event";
    var startDate = DateTime.UtcNow;
    var endDate = startDate.AddDays(7);
    
    // Act
    var evt = Event.Create(name, startDate, endDate);
    
    // Assert
    Assert.Single(evt.DomainEvents);
    var domainEvent = evt.DomainEvents.First();
    Assert.IsType<EventCreatedEvent>(domainEvent);
    
    var createdEvent = (EventCreatedEvent)domainEvent;
    Assert.Equal(evt.Id, createdEvent.EventId);
    Assert.Equal(name, createdEvent.EventName);
}

[Fact]
public void Expire_WhenActive_RaisesEventExpiredEvent()
{
    // Arrange
    var evt = Event.Create("Test", DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
    evt.ClearDomainEvents(); // Clear creation event
    
    // Act
    evt.Expire();
    
    // Assert
    Assert.Single(evt.DomainEvents);
    Assert.IsType<EventExpiredEvent>(evt.DomainEvents.First());
    Assert.Equal(EventStatus.Expired, evt.Status);
}

[Fact]
public void Close_WhenExpired_ThrowsException()
{
    // Arrange
    var evt = Event.Create("Test", DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
    evt.Expire();
    evt.ClearDomainEvents();
    
    // Act & Assert
    Assert.Throws<InvalidOperationException>(() => evt.Close());
    Assert.Empty(evt.DomainEvents); // No event raised on invalid operation
}
```

## Advanced Patterns

### Event Sourcing
Instead of storing current state, store all events:
```csharp
public class EventSourcedEntity
{
    private readonly List<DomainEvent> _events = new();
    
    public void Apply(DomainEvent evt)
    {
        // Update state based on event
        When(evt);
        _events.Add(evt);
    }
    
    public void Rehydrate(IEnumerable<DomainEvent> history)
    {
        foreach (var evt in history)
        {
            When(evt);
        }
    }
    
    private void When(DomainEvent evt)
    {
        switch (evt)
        {
            case EventCreatedEvent e:
                Id = e.EventId;
                Name = e.EventName;
                break;
            case EventExpiredEvent e:
                Status = EventStatus.Expired;
                break;
        }
    }
}
```

### Eventual Consistency
Domain events enable eventual consistency:
1. Update aggregate and raise events
2. Save aggregate
3. Publish events
4. Other aggregates/read models update asynchronously

## Anti-Patterns to Avoid

❌ **Don't include behavior in events** - Events are data, not operations
❌ **Don't publish domain events directly** - Convert to integration events
❌ **Don't make events mutable** - They represent facts
❌ **Don't raise too many events** - Keep them meaningful
❌ **Don't forget to clear events** - After publishing, clear collection
❌ **Don't couple events to infrastructure** - Keep in domain layer
❌ **Don't use events for synchronous validation** - Use domain methods

## Best Practices

✅ Name events in past tense
✅ Make events immutable
✅ Include relevant data (not full entity)
✅ Timestamp when event occurred
✅ Clear events after publishing
✅ Test entity behavior via events
✅ Document what each event means
✅ Keep events focused and specific
✅ Use events to trigger side effects
✅ Consider event versioning strategy
