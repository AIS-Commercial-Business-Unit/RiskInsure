# Messaging Patterns

## Overview

NServiceBus is the messaging framework that enables event-driven architecture. This document defines the patterns and conventions for commands, events, handlers, and message routing.

## Commands

### Definition
A **command** is an instruction sent to a specific endpoint to perform an action.

### Characteristics
- **Naming**: Imperative verbs (e.g., `RegisterEntity`, `ProcessOrder`, `ValidateData`)
- **Routing**: Unicast - sent to exactly one endpoint
- **Expectation**: Must succeed or throw an exception
- **Sending**: Use `context.Send()` or `context.SendLocal()`

### Command Structure
```csharp
// Domain/Contracts/Commands/ExecuteBusinessOperationCommand.cs
public class ExecuteBusinessOperationCommand
{
    public Guid EntityId { get; set; }
    public string RequiredData { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### Best Practices
- Use imperative verb + noun (e.g., `CreateEvent`, `UpdateStatus`)
- Include all data needed to execute
- Avoid unnecessary context or metadata
- Validate in handler, not in command class
- One command = one action

### Sending Commands

**From API:**
```csharp
// API publishes command to message bus
await _messageSession.Send(new ExecuteBusinessOperationCommand
{
    EntityId = Guid.NewGuid(),
    RequiredData = dto.Data,
    Timestamp = DateTime.UtcNow
});
```

**From Handler:**
```csharp
// Handler sends command locally or to another endpoint
await context.SendLocal(new ProcessNextStepCommand
{
    WorkflowId = message.WorkflowId
});
```

## Events

### Definition
An **event** is a notification that something has happened in the system.

### Characteristics
- **Naming**: Past tense (e.g., `EntityCreated`, `OrderProcessed`, `ValidationFailed`)
- **Routing**: Broadcast - published to all subscribers
- **Expectation**: Fire and forget, no direct response
- **Publishing**: Use `context.Publish()`

### Event Structure
```csharp
// Domain/Contracts/Events/OperationCompletedEvent.cs
public class OperationCompletedEvent
{
    public Guid EntityId { get; set; }
    public string ResultData { get; set; }
    public DateTime CompletedAt { get; set; }
}
```

### Best Practices
- Use past tense + noun (e.g., `EventCreated`, `StatusChanged`)
- Include only relevant data for subscribers
- Immutable - no setters, init-only properties preferred
- No behavior logic in event classes
- Multiple handlers can subscribe independently

### Publishing Events

**From Handler:**
```csharp
// After successful operation, publish event
await context.Publish(new OperationCompletedEvent
{
    EntityId = entity.Id,
    ResultData = entity.Result,
    CompletedAt = DateTime.UtcNow
});
```

## Domain Events vs Integration Events

### Domain Events
- **Location**: Raised within domain entities
- **Purpose**: Track state changes within the aggregate
- **Visibility**: Collected and published by infrastructure
- **Example**: `Event.Create()` raises `EventCreatedEvent`

### Integration Events
- **Location**: Defined in contracts, published by handlers
- **Purpose**: Notify other bounded contexts
- **Visibility**: Published to message bus for cross-domain communication
- **Example**: Handler publishes `EntityProcessedEvent` after saving

### Relationship
```
Domain Entity → Raises Domain Event → Handler Publishes Integration Event
```

## Handlers

### Command Handler
**Purpose**: Process a single command type

```csharp
public class ExecuteOperationCommandHandler : 
    IHandleMessages<ExecuteOperationCommand>
{
    private readonly IRepository _repository;
    private readonly ILogger<ExecuteOperationCommandHandler> _logger;
    
    public async Task Handle(
        ExecuteOperationCommand message, 
        IMessageHandlerContext context)
    {
        _logger.LogInformation("Processing operation: {Id}", message.EntityId);
        
        // 1. Validate
        // 2. Load/create entity
        // 3. Execute business logic
        // 4. Save changes
        // 5. Publish event
        
        await context.Publish(new OperationCompletedEvent
        {
            EntityId = message.EntityId,
            CompletedAt = DateTime.UtcNow
        });
    }
}
```

### Event Handler
**Purpose**: React to an event, can have multiple handlers per event

```csharp
public class OperationCompletedEventHandler : 
    IHandleMessages<OperationCompletedEvent>
{
    private readonly INotificationService _notifications;
    
    public async Task Handle(
        OperationCompletedEvent message, 
        IMessageHandlerContext context)
    {
        // React to event - this is one of potentially many handlers
        await _notifications.NotifyAsync(message.EntityId);
    }
}
```

### Handler Best Practices
- One handler per message type
- Keep handlers focused and small
- Handle failures appropriately (throw for business errors)
- Log at appropriate levels
- Don't catch exceptions in business handlers (let NServiceBus retry)
- Catch exceptions in notification handlers (don't fail business flow)

## Message Routing

### Local Routing
Commands sent within the same endpoint:
```csharp
await context.SendLocal(new LocalCommand());
```

### Remote Routing
Commands sent to specific endpoint:
```csharp
await context.Send(new RemoteCommand());
// Routing configured in endpoint configuration
```

### Publish/Subscribe
Events automatically routed to all subscribers:
```csharp
await context.Publish(new SomethingHappenedEvent());
// All endpoints subscribing to this event receive it
```

## Message Contracts Location

### InternalContracts Project
- **Purpose**: Shared message contracts within this bounded context
- **Contains**: Commands and integration events used internally
- **Referenced By**: Api, Message, Infrastructure projects

### Public Contracts
- **Package**: `AcmeTickets.Contracts.Public`
- **Purpose**: Cross-domain integration events
- **Contains**: Events published for other bounded contexts
- **Consumed By**: Multiple domains

## Error Handling

### Retry Policy
- **Immediate Retries**: 3 attempts with minimal delay
- **Delayed Retries**: Exponential backoff for transient failures
- **Configuration**: Defined in endpoint configuration

### Dead Letter Queue
- **Purpose**: Store messages that exceed retry limits
- **Action Required**: Manual investigation and resolution
- **Access**: Via Azure Service Bus Explorer or monitoring tools

### Poison Messages
- **Detection**: Messages that consistently fail processing
- **Handling**: Moved to error queue after exhausting retries
- **Resolution**: Fix issue, retry from error queue

## Saga Pattern

### When to Use Sagas
- Long-running workflows spanning multiple steps
- State must be maintained across messages
- Compensating actions needed for failures
- Example: Multi-step approval process

### Saga Structure
```csharp
public class WorkflowSaga : Saga<WorkflowSagaData>,
    IAmStartedByMessages<StartWorkflow>,
    IHandleMessages<StepCompleted>
{
    protected override void ConfigureHowToFindSaga(
        SagaPropertyMapper<WorkflowSagaData> mapper)
    {
        mapper.MapSaga(saga => saga.WorkflowId)
            .ToMessage<StartWorkflow>(msg => msg.WorkflowId)
            .ToMessage<StepCompleted>(msg => msg.WorkflowId);
    }
    
    public async Task Handle(StartWorkflow message, IMessageHandlerContext context)
    {
        Data.WorkflowId = message.WorkflowId;
        Data.Status = "InProgress";
        
        await context.Send(new ExecuteFirstStep { WorkflowId = message.WorkflowId });
    }
    
    public async Task Handle(StepCompleted message, IMessageHandlerContext context)
    {
        Data.StepsCompleted++;
        
        if (Data.StepsCompleted == Data.TotalSteps)
        {
            await context.Publish(new WorkflowCompleted { WorkflowId = Data.WorkflowId });
            MarkAsComplete();
        }
    }
}
```

## Message Design Principles

### Keep Messages Small
- Include only essential data
- Reference entities by ID, don't embed full objects
- Consider message size limits

### Make Messages Immutable
- Use `init` properties or readonly fields
- No setters after construction
- Prevents accidental modification

### Versioning Strategy
- Add new properties as optional
- Never remove properties (mark obsolete instead)
- Use message type names for major versions

### Correlation
- Include correlation IDs for tracking
- NServiceBus automatically propagates conversation IDs
- Use for distributed tracing and debugging

## Testing Message Handlers

### Unit Testing
```csharp
[Fact]
public async Task Handler_ProcessesCommand_Successfully()
{
    // Arrange
    var mockRepository = new Mock<IRepository>();
    var handler = new ExecuteOperationCommandHandler(mockRepository.Object);
    var message = new ExecuteOperationCommand { EntityId = Guid.NewGuid() };
    var context = new TestableMessageHandlerContext();
    
    // Act
    await handler.Handle(message, context);
    
    // Assert
    Assert.Single(context.PublishedMessages);
    mockRepository.Verify(r => r.SaveAsync(It.IsAny<Entity>()), Times.Once);
}
```

### Integration Testing
- Use NServiceBus testing framework
- Test with real transport (in-memory or Azure Service Bus)
- Verify message routing and handler invocation

## Common Patterns

### Request-Reply
```csharp
// Send command and wait for response (not typical, prefer events)
var response = await context.Request<ResponseMessage>(new RequestCommand());
```

### Deferred Messages
```csharp
// Schedule message for future delivery
var sendOptions = new SendOptions();
sendOptions.DelayDeliveryWith(TimeSpan.FromHours(24));
await context.Send(new ReminderCommand(), sendOptions);
```

### Outbox Pattern
- Ensures message publishing happens atomically with data changes
- Prevents partial updates (data saved but message not published)
- Configured at endpoint level

## Anti-Patterns to Avoid

❌ **Don't use commands for notifications** - Use events instead
❌ **Don't include business logic in message classes** - Keep them as DTOs
❌ **Don't create circular message flows** - Can cause infinite loops
❌ **Don't publish commands** - Commands are sent, events are published
❌ **Don't handle multiple unrelated messages in one handler** - One handler per message
❌ **Don't use messages for synchronous calls** - Use direct method calls in same process
