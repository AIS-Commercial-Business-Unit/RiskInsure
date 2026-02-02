# Naming Conventions

## Overview

Consistent naming conventions improve code readability, maintainability, and collaboration. This document defines naming standards for all code elements in the bounded context.

## General Principles

1. **Use PascalCase** for types, methods, properties, constants
2. **Use camelCase** for parameters, local variables, private fields
3. **Use meaningful, descriptive names** over abbreviations
4. **Avoid Hungarian notation** (no type prefixes like `strName`, `intCount`)
5. **Be consistent** within the codebase

## Namespace Patterns

### Standard Structure
```
{Company}.{Product}.{BoundedContext}.{Layer}.{Feature}
```

### Examples
```csharp
EventManagement.Domain.Entities
EventManagement.Domain.Events
EventManagement.Domain.Repositories
EventManagement.Application.Services
EventManagement.Application.Commands
EventManagement.Application.DTOs
EventManagement.Infrastructure.Repositories
EventManagement.Infrastructure.MessageHandlers
AcmeTickets.EventManagement.InternalContracts.Events
AcmeTickets.Message.Handlers
```

### For Multi-Word Domains
```csharp
OrderManagement.Domain.Entities
CustomerService.Application.Commands
PaymentProcessing.Infrastructure.Repositories
```

## Project Naming

### Pattern
```
{BoundedContext}.{Layer}
```

### Examples
```
EventManagement.Domain
EventManagement.Application
EventManagement.Infrastructure
EventManagement.Api
EventManagement.App
EventManagement.Message
EventManagement.InternalContracts
EventManagement.Test.UnitTests
EventManagement.Test.Mocks
```

## File and Folder Structure

### One Type Per File
```
✅ Event.cs contains class Event
✅ EventStatus.cs contains enum EventStatus
❌ EventTypes.cs contains Event, EventStatus, EventDto
```

### Folder Naming
- Use **PascalCase** for folders
- Match namespace structure
```
Domain/
  Entities/
    Event.cs
    EventStatus.cs
  Events/
    EventCreatedEvent.cs
    EventExpiredEvent.cs
  Repositories/
    IEventRepository.cs
```

## Entities and Value Objects

### Entity Classes
**Pattern**: Singular noun, PascalCase
```csharp
✅ Event
✅ Customer
✅ OrderItem
❌ Events
❌ event
❌ TEvent
```

### Value Objects
**Pattern**: Descriptive noun
```csharp
✅ Address
✅ Money
✅ DateRange
```

### Enumerations
**Pattern**: Singular noun, PascalCase values
```csharp
public enum EventStatus
{
    Active,
    Expired,
    Closed,
    Cancelled
}

public enum OrderState
{
    Pending,
    Confirmed,
    Shipped,
    Delivered
}
```

## Commands and Events

### Commands (Imperative)
**Pattern**: Verb + Noun + "Command"
```csharp
✅ CreateEventCommand
✅ UpdateEventCommand
✅ RegisterBeneficiaryCommand
✅ ProcessPaymentCommand
❌ EventCreate
❌ CreatingEvent
❌ CreateEventMsg
```

### Events (Past Tense)
**Pattern**: Noun + Verb (past tense) + "Event"
```csharp
✅ EventCreatedEvent
✅ EventExpiredEvent
✅ PaymentProcessedEvent
✅ BeneficiaryRegisteredEvent
❌ CreateEvent
❌ CreatingEvent
❌ EventCreate
```

### Domain Events
Same naming as integration events, located in `Domain/Events/`
```csharp
EventCreatedEvent
EventExpiredEvent
EventClosedEvent
```

### Integration Events
May include "Integration" suffix for clarity when needed
```csharp
EventCreatedIntegrationEvent  // When distinction needed
EventCreatedEvent             // Simpler when context is clear
```

## Interfaces

### Pattern
**Prefix with "I"**, describe capability or responsibility
```csharp
✅ IEventRepository
✅ IMessagePublisher
✅ IValidator
✅ IExternalService
❌ EventRepository (interface)
❌ RepositoryInterface
```

### Repository Interfaces
```csharp
IEventRepository
ICustomerRepository
IOrderRepository
```

### Service Interfaces
```csharp
IEventService
INotificationService
IPaymentService
```

## Repository Implementations

### Pattern
**{Entity}Repository**
```csharp
✅ EventRepository : IEventRepository
✅ CustomerRepository : ICustomerRepository
❌ EventRepo
❌ EventRepositoryImpl
❌ CosmosEventRepository (unless multiple implementations)
```

### Multiple Implementations
When multiple implementations exist, use descriptive prefix:
```csharp
CosmosEventRepository : IEventRepository
SqlEventRepository : IEventRepository
InMemoryEventRepository : IEventRepository
```

## Message Handlers

### Command Handlers
**Pattern**: {Command}Handler
```csharp
✅ CreateEventCommandHandler
✅ UpdateEventCommandHandler
✅ ProcessPaymentCommandHandler
❌ CreateEventHandler
❌ EventCreationHandler
```

### Event Handlers
**Pattern**: {Event}Handler
```csharp
✅ EventCreatedEventHandler
✅ PaymentProcessedEventHandler
✅ BeneficiaryRegisteredEventHandler
```

### Multiple Event Handlers
When multiple handlers for same event, add descriptive suffix:
```csharp
EventCreatedEventHandler              // Primary business logic
EventCreatedNotificationHandler       // Sends notifications
EventCreatedAuditHandler              // Logs for audit
EventCreatedAnalyticsHandler          // Updates analytics
```

## Services

### Application Services
**Pattern**: {Entity}Service or {Feature}Service
```csharp
✅ EventService
✅ CustomerService
✅ ValidationService
✅ NotificationService
❌ EventApplicationService
❌ ServiceForEvents
```

### Domain Services
**Pattern**: {Entity}{Capability}Service
```csharp
✅ EventValidationService
✅ PaymentCalculationService
✅ OrderPricingService
```

## DTOs (Data Transfer Objects)

### Request DTOs
**Pattern**: {Action}{Entity}Request
```csharp
✅ CreateEventRequest
✅ UpdateEventRequest
✅ RegisterBeneficiaryRequest
❌ EventCreateDto
❌ CreateEventDto
❌ EventRequest
```

### Response DTOs
**Pattern**: {Entity}Response or {Entity}Dto
```csharp
✅ EventResponse
✅ CreateEventResponse
✅ EventDto
✅ EventListItemDto
❌ EventResponseDto
❌ EventOutput
```

### List/Collection DTOs
```csharp
✅ EventListItemDto
✅ CustomerSummaryDto
✅ OrderLineItemDto
```

## Controllers/Endpoints

### API Controllers
**Pattern**: {Entity}Controller (plural)
```csharp
✅ EventsController
✅ CustomersController
✅ OrdersController
❌ EventController (singular)
❌ EventApiController
```

### Minimal API Endpoints
**Pattern**: {entity} (lowercase, plural in route)
```csharp
✅ app.MapPost("/api/events", ...)
✅ app.MapGet("/api/events/{id}", ...)
❌ app.MapPost("/api/Event", ...)
```

## Variables and Parameters

### Local Variables
**Pattern**: camelCase, descriptive
```csharp
✅ var eventId = Guid.NewGuid();
✅ var startDate = DateTime.UtcNow;
✅ var customerName = "John Doe";
❌ var id = Guid.NewGuid();  // Too vague
❌ var sd = DateTime.UtcNow;  // Abbreviated
```

### Method Parameters
**Pattern**: camelCase
```csharp
public async Task<Event> GetByIdAsync(Guid eventId)
public void UpdateStatus(EventStatus newStatus)
public Event Create(string eventName, DateTime startDate, DateTime endDate)
```

### Private Fields
**Pattern**: _camelCase with underscore prefix
```csharp
private readonly IEventRepository _repository;
private readonly ILogger<EventService> _logger;
private readonly List<DomainEvent> _domainEvents;
```

## Constants

### Pattern
**PascalCase** or **UPPER_SNAKE_CASE** (choose one consistently)
```csharp
// PascalCase (preferred for .NET)
public const int MaxNameLength = 200;
public const string DefaultStatus = "Active";

// Or UPPER_SNAKE_CASE (if team preference)
public const int MAX_NAME_LENGTH = 200;
public const string DEFAULT_STATUS = "Active";
```

## Methods

### Pattern
**PascalCase**, verb or verb phrase
```csharp
✅ GetByIdAsync()
✅ SaveAsync()
✅ Create()
✅ ValidateBusinessRules()
✅ CalculateTotalPrice()
❌ get()
❌ GetData()  // Too vague
❌ DoStuff()
```

### Async Methods
Suffix with **Async**
```csharp
✅ GetByIdAsync()
✅ SaveAsync()
✅ ProcessPaymentAsync()
❌ GetById() // When actually async
❌ GetByIdTaskAsync() // Redundant
```

### Boolean Methods/Properties
Prefix with **Is**, **Has**, **Can**, **Should**
```csharp
✅ IsActive()
✅ HasValidStatus()
✅ CanExpire()
✅ ShouldNotify()
❌ Active() // Not clear it returns bool
❌ Check() // Too vague
```

## Properties

### Pattern
**PascalCase**, noun or adjective
```csharp
✅ public Guid Id { get; set; }
✅ public string Name { get; set; }
✅ public EventStatus Status { get; set; }
✅ public bool IsActive { get; set; }
✅ public DateTime CreatedAt { get; set; }
❌ public Guid id { get; set; }
❌ public string name { get; set; }
```

### Collection Properties
Use plural names
```csharp
✅ public ICollection<OrderItem> OrderItems { get; set; }
✅ public List<DomainEvent> DomainEvents { get; }
❌ public List<DomainEvent> DomainEventList { get; }
```

## Test Classes and Methods

### Test Class
**Pattern**: {ClassUnderTest}Tests
```csharp
✅ EventTests
✅ EventServiceTests
✅ CreateEventCommandHandlerTests
❌ TestEvent
❌ EventTestClass
```

### Test Methods
**Pattern**: MethodName_Scenario_ExpectedBehavior
```csharp
✅ Create_ValidParameters_CreatesEvent()
✅ Expire_WhenActive_ChangesStatusToExpired()
✅ GetById_NonExistingId_ReturnsNull()
❌ TestCreate()
❌ Test1()
❌ CreateTest()
```

## Configuration Classes

### Pattern
**{Feature}Configuration** or **{Feature}Options**
```csharp
✅ DatabaseConfiguration
✅ MessagingConfiguration
✅ CosmosDbOptions
✅ ServiceBusOptions
❌ Config
❌ Settings
```

## Exceptions

### Pattern
**{Specific}Exception**, inherit from appropriate base
```csharp
✅ EventNotFoundException : NotFoundException
✅ InvalidEventStatusException : DomainException
✅ PaymentFailedException : ApplicationException
❌ EventException // Too generic
❌ MyException
```

## Extension Methods

### Pattern
**{Type}Extensions**, descriptive method names
```csharp
// File: StringExtensions.cs
public static class StringExtensions
{
    public static bool IsNullOrWhiteSpace(this string value)
    {
        return string.IsNullOrWhiteSpace(value);
    }
}

// File: EventExtensions.cs
public static class EventExtensions
{
    public static bool IsExpired(this Event evt)
    {
        return evt.Status == EventStatus.Expired;
    }
}
```

## Attribute Usage

### Data Annotations
```csharp
[Required]
[StringLength(200)]
[Range(1, 100)]
[EmailAddress]
```

### JSON Serialization
```csharp
[JsonProperty("id")]
[JsonPropertyName("eventName")]
[JsonIgnore]
```

### HTTP Attributes
```csharp
[HttpGet]
[HttpPost]
[Route("api/events/{id}")]
[ProducesResponseType(StatusCodes.Status200OK)]
```

## Common Abbreviations (Avoid Unless Standard)

### Acceptable
- `Id` (identifier)
- `Dto` (data transfer object)
- `Api` (application programming interface)
- `Url` (uniform resource locator)
- `Html` (hypertext markup language)
- `Json` (JavaScript object notation)

### Avoid
- `Evt` → Use `Event`
- `Cmd` → Use `Command`
- `Msg` → Use `Message`
- `Ctx` → Use `Context`
- `Svc` → Use `Service`
- `Repo` → Use `Repository`
- `Cfg` → Use `Configuration`

## Anti-Patterns to Avoid

❌ **Generic names**: `Manager`, `Helper`, `Utility`, `Common`
❌ **Type suffixes in variables**: `eventClass`, `statusEnum`
❌ **Redundant naming**: `EventEvent`, `EventDomainEvent`
❌ **Inconsistent casing**: mixing PascalCase and camelCase in same context
❌ **Unclear abbreviations**: `e`, `tmp`, `data`, `obj`
❌ **Numbers in names**: `Event2`, `ProcessMethod3`
❌ **Underscores in public members**: `public int _count`

## Best Practices Summary

✅ **Be descriptive** - Name reveals intent
✅ **Be consistent** - Follow patterns throughout codebase
✅ **Be concise** - Don't be overly verbose
✅ **Use domain language** - Match business terminology
✅ **Avoid abbreviations** - Unless widely understood
✅ **Follow .NET conventions** - PascalCase for public, camelCase for private
✅ **Use singular for types** - Plural for collections
✅ **Prefix interfaces with I** - Standard .NET practice
✅ **Suffix async methods** - Append "Async"
✅ **Name tests clearly** - MethodName_Scenario_ExpectedBehavior
