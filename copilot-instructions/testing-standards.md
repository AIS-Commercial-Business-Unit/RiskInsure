# Testing Standards

## Overview

This document defines testing standards and patterns for the bounded context, covering unit tests, integration tests, and test project organization.

## Test Project Structure

### Test.UnitTests
**Purpose**: Fast, isolated tests for domain logic and application services

**Structure**:
```
Test.UnitTests/
├── Domain/
│   ├── EntityTests.cs
│   ├── EventTests.cs
│   └── ValidationTests.cs
├── Application/
│   ├── ServiceTests.cs
│   └── CommandTests.cs
└── Test.UnitTests.csproj
```

### Test.Mocks
**Purpose**: Reusable fake implementations and test doubles

**Structure**:
```
Test.Mocks/
├── Fakes/
│   ├── FakeEventRepository.cs
│   ├── FakeExternalService.cs
│   └── FakeSenderService.cs
└── Test.Mocks.csproj
```

## Testing Framework

### xUnit
Primary testing framework for all tests

**Key Features**:
- `[Fact]`: Simple test without parameters
- `[Theory]`: Parameterized test with data
- `[InlineData]`: Inline test data
- `IClassFixture<T>`: Shared test context
- `ICollectionFixture<T>`: Shared across test classes

### Assertion Library
- xUnit built-in assertions
- FluentAssertions (optional for readability)

### Mocking
- Moq for creating mocks (when needed)
- Prefer fakes over mocks when practical

## Unit Testing Domain Layer

### Testing Domain Entities

```csharp
// Test.UnitTests/Domain/EventTests.cs
public class EventTests
{
    [Fact]
    public void Create_ValidParameters_CreatesEvent()
    {
        // Arrange
        var name = "Test Event";
        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddDays(7);
        
        // Act
        var evt = Event.Create(name, startDate, endDate);
        
        // Assert
        Assert.NotEqual(Guid.Empty, evt.Id);
        Assert.Equal(name, evt.Name);
        Assert.Equal(startDate, evt.StartDate);
        Assert.Equal(endDate, evt.EndDate);
        Assert.Equal(EventStatus.Active, evt.Status);
    }
    
    [Fact]
    public void Expire_WhenActive_ChangesStatusToExpired()
    {
        // Arrange
        var evt = Event.Create("Test", DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
        
        // Act
        evt.Expire();
        
        // Assert
        Assert.Equal(EventStatus.Expired, evt.Status);
    }
    
    [Fact]
    public void Expire_WhenNotActive_ThrowsInvalidOperationException()
    {
        // Arrange
        var evt = Event.Create("Test", DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
        evt.Expire();
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => evt.Expire());
    }
    
    [Theory]
    [InlineData(EventStatus.Expired)]
    [InlineData(EventStatus.Closed)]
    public void Close_WhenNotActive_ThrowsInvalidOperationException(EventStatus status)
    {
        // Arrange
        var evt = Event.Create("Test", DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
        if (status == EventStatus.Expired) evt.Expire();
        if (status == EventStatus.Closed) evt.Close();
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => evt.Close());
    }
}
```

### Testing Domain Events

```csharp
public class EventDomainEventsTests
{
    [Fact]
    public void Create_RaisesEventCreatedEvent()
    {
        // Arrange & Act
        var evt = Event.Create("Test", DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
        
        // Assert
        Assert.Single(evt.DomainEvents);
        var domainEvent = evt.DomainEvents.First();
        Assert.IsType<EventCreatedEvent>(domainEvent);
    }
    
    [Fact]
    public void Expire_RaisesEventExpiredEvent()
    {
        // Arrange
        var evt = Event.Create("Test", DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
        evt.ClearDomainEvents();
        
        // Act
        evt.Expire();
        
        // Assert
        Assert.Single(evt.DomainEvents);
        Assert.IsType<EventExpiredEvent>(evt.DomainEvents.First());
    }
}
```

## Unit Testing Application Layer

### Testing Application Services

```csharp
// Test.UnitTests/Application/EventServiceTests.cs
public class EventServiceTests
{
    private readonly Mock<IEventRepository> _mockRepository;
    private readonly EventService _service;
    
    public EventServiceTests()
    {
        _mockRepository = new Mock<IEventRepository>();
        _service = new EventService(_mockRepository.Object);
    }
    
    [Fact]
    public async Task CreateEvent_ValidData_SavesAndReturnsDto()
    {
        // Arrange
        var command = new CreateEventCommand
        {
            Name = "Test Event",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7)
        };
        
        // Act
        var result = await _service.CreateEventAsync(command);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(command.Name, result.Name);
        _mockRepository.Verify(
            r => r.SaveAsync(It.IsAny<Event>()), 
            Times.Once);
    }
    
    [Fact]
    public async Task GetEvent_ExistingId_ReturnsDto()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var evt = Event.Create("Test", DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
        _mockRepository
            .Setup(r => r.GetByIdAsync(eventId))
            .ReturnsAsync(evt);
        
        // Act
        var result = await _service.GetEventAsync(eventId);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(evt.Name, result.Name);
    }
    
    [Fact]
    public async Task GetEvent_NonExistingId_ReturnsNull()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByIdAsync(eventId))
            .ReturnsAsync((Event)null);
        
        // Act
        var result = await _service.GetEventAsync(eventId);
        
        // Assert
        Assert.Null(result);
    }
}
```

## Creating Fake Implementations

### Fake Repository

```csharp
// Test.Mocks/Fakes/FakeEventRepository.cs
public class FakeEventRepository : IEventRepository
{
    private readonly Dictionary<Guid, Event> _events = new();
    
    public Task<Event> GetByIdAsync(Guid id)
    {
        _events.TryGetValue(id, out var evt);
        return Task.FromResult(evt);
    }
    
    public Task<IEnumerable<Event>> GetAllAsync()
    {
        return Task.FromResult(_events.Values.AsEnumerable());
    }
    
    public Task SaveAsync(Event entity)
    {
        _events[entity.Id] = entity;
        return Task.CompletedTask;
    }
    
    public Task DeleteAsync(Guid id)
    {
        _events.Remove(id);
        return Task.CompletedTask;
    }
    
    // Helper methods for tests
    public void Clear() => _events.Clear();
    public int Count => _events.Count;
    public bool Contains(Guid id) => _events.ContainsKey(id);
}
```

### Using Fakes

```csharp
public class EventServiceWithFakeTests
{
    [Fact]
    public async Task CreateAndRetrieve_WorksCorrectly()
    {
        // Arrange
        var fakeRepo = new FakeEventRepository();
        var service = new EventService(fakeRepo);
        var command = new CreateEventCommand
        {
            Name = "Test",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7)
        };
        
        // Act
        var created = await service.CreateEventAsync(command);
        var retrieved = await service.GetEventAsync(created.Id);
        
        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(created.Name, retrieved.Name);
        Assert.Equal(1, fakeRepo.Count);
    }
}
```

## Testing Message Handlers

### Handler Tests with Mocks

```csharp
public class CreateEventCommandHandlerTests
{
    private readonly Mock<IEventRepository> _mockRepository;
    private readonly Mock<IMessageHandlerContext> _mockContext;
    private readonly CreateEventCommandHandler _handler;
    
    public CreateEventCommandHandlerTests()
    {
        _mockRepository = new Mock<IEventRepository>();
        _mockContext = new Mock<IMessageHandlerContext>();
        _handler = new CreateEventCommandHandler(_mockRepository.Object);
    }
    
    [Fact]
    public async Task Handle_ValidCommand_SavesEventAndPublishesEvent()
    {
        // Arrange
        var command = new CreateEventCommand
        {
            EventId = Guid.NewGuid(),
            Name = "Test Event",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7)
        };
        
        // Act
        await _handler.Handle(command, _mockContext.Object);
        
        // Assert
        _mockRepository.Verify(
            r => r.SaveAsync(It.Is<Event>(e => 
                e.Id == command.EventId && 
                e.Name == command.Name)),
            Times.Once);
        
        _mockContext.Verify(
            c => c.Publish(It.IsAny<EventCreatedEvent>(), It.IsAny<PublishOptions>()),
            Times.Once);
    }
}
```

## Integration Testing

### Cosmos DB Integration Tests

```csharp
public class EventRepositoryIntegrationTests : IDisposable
{
    private readonly CosmosClient _client;
    private readonly EventRepository _repository;
    private readonly Database _database;
    
    public EventRepositoryIntegrationTests()
    {
        // Use Cosmos DB Emulator
        _client = new CosmosClient(
            "https://localhost:8081",
            "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
        
        _database = _client.CreateDatabaseIfNotExistsAsync("TestDb").Result.Database;
        var container = _database.CreateContainerIfNotExistsAsync(
            new ContainerProperties("Events", "/id")).Result.Container;
        
        _repository = new EventRepository(_client, NullLogger<EventRepository>.Instance);
    }
    
    [Fact]
    public async Task SaveAndRetrieve_WorksCorrectly()
    {
        // Arrange
        var evt = Event.Create("Test Event", DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
        
        // Act
        await _repository.SaveAsync(evt);
        var retrieved = await _repository.GetByIdAsync(evt.Id);
        
        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(evt.Name, retrieved.Name);
        Assert.Equal(evt.Status, retrieved.Status);
    }
    
    [Fact]
    public async Task Delete_RemovesEntity()
    {
        // Arrange
        var evt = Event.Create("Test Event", DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
        await _repository.SaveAsync(evt);
        
        // Act
        await _repository.DeleteAsync(evt.Id);
        var retrieved = await _repository.GetByIdAsync(evt.Id);
        
        // Assert
        Assert.Null(retrieved);
    }
    
    public void Dispose()
    {
        _database.DeleteAsync().Wait();
        _client.Dispose();
    }
}
```

### API Integration Tests

```csharp
public class EventsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    
    public EventsApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }
    
    [Fact]
    public async Task CreateEvent_ValidRequest_Returns202()
    {
        // Arrange
        var request = new CreateEventRequest
        {
            Name = "Test Event",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7)
        };
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/events", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
    
    [Fact]
    public async Task CreateEvent_InvalidRequest_Returns400()
    {
        // Arrange
        var request = new CreateEventRequest
        {
            // Name missing
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7)
        };
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/events", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

## Test Naming Conventions

### Pattern
```
MethodName_Scenario_ExpectedBehavior
```

### Examples
```csharp
Create_ValidParameters_CreatesEvent()
Expire_WhenActive_ChangesStatusToExpired()
Expire_WhenNotActive_ThrowsInvalidOperationException()
Handle_ValidCommand_SavesEventAndPublishesEvent()
GetEvent_NonExistingId_ReturnsNull()
```

## Test Organization

### Arrange-Act-Assert Pattern
```csharp
[Fact]
public void ExampleTest()
{
    // Arrange - Set up test data and dependencies
    var input = "test";
    
    // Act - Execute the behavior being tested
    var result = MethodUnderTest(input);
    
    // Assert - Verify the expected outcome
    Assert.Equal("expected", result);
}
```

### One Assert Per Test (Guideline)
Prefer focused tests with single assertions:
```csharp
[Fact]
public void Create_SetsId() 
{
    var evt = Event.Create("Test", DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
    Assert.NotEqual(Guid.Empty, evt.Id);
}

[Fact]
public void Create_SetsName() 
{
    var name = "Test Event";
    var evt = Event.Create(name, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
    Assert.Equal(name, evt.Name);
}
```

## What to Test

### Domain Layer ✅
- Entity creation
- Business logic and invariants
- State transitions
- Validation rules
- Domain events raised
- Edge cases and error conditions

### Application Layer ✅
- Service orchestration
- DTO mapping
- Command handling flow
- Error handling

### API Layer ✅
- Request validation
- Response status codes
- Error responses
- Content types

### Infrastructure Layer ✅
- Repository implementations (integration tests)
- Data mapping
- External service integrations

## What NOT to Test

### Don't Test ❌
- Framework code (ASP.NET, NServiceBus)
- Third-party libraries
- Simple getters/setters without logic
- Auto-generated code
- Private methods (test through public interface)

## Test Data Builders

### Builder Pattern for Complex Objects

```csharp
public class EventBuilder
{
    private string _name = "Test Event";
    private DateTime _startDate = DateTime.UtcNow;
    private DateTime _endDate = DateTime.UtcNow.AddDays(7);
    
    public EventBuilder WithName(string name)
    {
        _name = name;
        return this;
    }
    
    public EventBuilder WithDates(DateTime start, DateTime end)
    {
        _startDate = start;
        _endDate = end;
        return this;
    }
    
    public Event Build()
    {
        return Event.Create(_name, _startDate, _endDate);
    }
}

// Usage
var evt = new EventBuilder()
    .WithName("Custom Event")
    .WithDates(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1))
    .Build();
```

## Test Coverage Goals

### Minimum Coverage
- **Domain Layer**: 90%+ (business logic is critical)
- **Application Layer**: 80%+
- **API Layer**: 70%+
- **Infrastructure Layer**: 60%+ (focus on complex logic)

### Measuring Coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

## Continuous Integration

### Test Execution in CI/CD
```yaml
# In GitHub Actions workflow
- name: Run Tests
  run: dotnet test --no-build --verbosity normal
```

### Test Categories
```csharp
[Trait("Category", "Unit")]
public class FastTests { }

[Trait("Category", "Integration")]
public class SlowTests { }
```

```bash
# Run only unit tests
dotnet test --filter "Category=Unit"
```

## Best Practices

### Do's ✅
1. Write tests first (TDD) or immediately after code
2. Keep tests fast (especially unit tests)
3. Make tests independent and isolated
4. Use descriptive test names
5. Follow AAA pattern
6. Test edge cases and error conditions
7. Use fakes for external dependencies
8. Clean up resources (IDisposable)
9. Maintain tests like production code
10. Run tests before committing

### Don'ts ❌
1. Don't test implementation details
2. Don't create interdependent tests
3. Don't ignore failing tests
4. Don't skip cleanup
5. Don't use Thread.Sleep (use async properly)
6. Don't test private methods directly
7. Don't duplicate production logic in tests
8. Don't make tests too complex
9. Don't commit commented-out tests
10. Don't skip integration tests for critical paths

## Tools and Libraries

### Testing Frameworks
- xUnit.net
- Moq (mocking)
- FluentAssertions (optional)

### Test Runners
- Visual Studio Test Explorer
- `dotnet test` CLI
- Rider test runner

### Coverage Tools
- Coverlet
- ReportGenerator
- Codecov (for CI integration)
