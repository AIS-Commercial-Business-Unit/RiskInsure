# Data Patterns

## Overview

This bounded context uses Azure Cosmos DB as the primary database following the principle of **one database per domain**. This ensures clear data ownership, independent scaling, and bounded context isolation.

## Core Principles

### 1. One Database Per Domain
Each bounded context has its own Cosmos DB database:
- Clear data ownership and boundaries
- Independent schema evolution
- Separate scaling and performance tuning
- Isolation of failures

### 2. Repository Pattern
All database access goes through repository interfaces:

**Domain defines the contract:**
```csharp
// Domain/Repositories/IEntityRepository.cs
public interface IEntityRepository
{
    Task<Entity> GetByIdAsync(Guid id);
    Task<IEnumerable<Entity>> GetAllAsync();
    Task SaveAsync(Entity entity);
    Task DeleteAsync(Guid id);
}
```

**Infrastructure implements:**
```csharp
// Infrastructure/Repositories/EntityRepository.cs
public class EntityRepository : IEntityRepository
{
    private readonly Container _container;
    
    public async Task<Entity> GetByIdAsync(Guid id)
    {
        var document = await _container.ReadItemAsync<EntityDocument>(
            id.ToString(), 
            new PartitionKey(id.ToString()));
        return MapToDomain(document.Resource);
    }
}
```

**Benefits:**
- Testable (mock repository in tests)
- Swappable (change database without changing domain)
- Clear separation of concerns

### 3. CQRS Pattern (Optional)
Separate read models from write models when needed:

**Write Model (Command Side):**
```csharp
public class Entity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    // Rich domain logic, validation
}
```

**Read Model (Query Side):**
```csharp
public class EntityListItem
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; }
    // Optimized for display, denormalized
}
```

**When to Use CQRS:**
- Read and write requirements differ significantly
- Performance optimization needed
- Complex queries with aggregations
- Different consistency requirements

## Cosmos DB Database Structure

### Container Organization

Typical containers per domain:
- **Primary Entity Container(s)**: Business entities
- **Sagas Container**: NServiceBus saga data
- **Leases Container**: Change feed tracking (if using)

### Example Structure
```
CosmosDB Account
└── DomainNameDb
    ├── Entities (container)
    ├── Sagas (container)
    └── Leases (container)
```

## Partition Key Strategy

### What is a Partition Key?
- Cosmos DB distributes data across physical partitions
- Partition key determines data distribution
- Critical for performance and scalability

### Common Strategies

**Strategy 1: Entity ID**
```csharp
PartitionKey = /id
```
- Simple but limits query performance
- Each partition contains single document
- Use only for high-volume, ID-based lookups

**Strategy 2: Type-Based**
```csharp
PartitionKey = /entityType
```
- Groups similar entities together
- Good for small to medium datasets
- Enables efficient cross-document queries

**Strategy 3: Tenant/Customer ID**
```csharp
PartitionKey = /tenantId
```
- Ideal for multi-tenant scenarios
- Natural data isolation
- Scales with number of tenants

**Strategy 4: Composite**
```csharp
PartitionKey = /tenantId-entityType
```
- Balances distribution and query efficiency
- Combines benefits of multiple strategies

### Choosing a Strategy
- Analyze access patterns
- Consider data volume
- Balance between distribution and query efficiency
- Include partition key in all queries for best performance

## Document Structure

### Mapping Domain to Database

**Domain Entity:**
```csharp
public class Event
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DateTime StartDate { get; set; }
    public EventStatus Status { get; set; }
}
```

**Database Document:**
```csharp
public class EventDocument
{
    [JsonProperty("id")]
    public string Id { get; set; }
    
    [JsonProperty("partitionKey")]
    public string PartitionKey { get; set; }
    
    [JsonProperty("name")]
    public string Name { get; set; }
    
    [JsonProperty("startDate")]
    public DateTime StartDate { get; set; }
    
    [JsonProperty("status")]
    public string Status { get; set; }
    
    [JsonProperty("_etag")]
    public string ETag { get; set; }
}
```

### Best Practices
- Use separate document classes (don't reuse domain entities)
- Use `[JsonProperty]` for lowercase, consistent naming
- Store enums as strings for readability
- Include `_etag` for optimistic concurrency

## Repository Implementation

### Full Repository Example
```csharp
public class EventRepository : IEventRepository
{
    private readonly Container _container;
    private readonly ILogger<EventRepository> _logger;
    
    public EventRepository(CosmosClient client, ILogger<EventRepository> logger)
    {
        var database = client.GetDatabase("EventManagementDb");
        _container = database.GetContainer("Events");
        _logger = logger;
    }
    
    public async Task<Event> GetByIdAsync(Guid id)
    {
        try
        {
            var response = await _container.ReadItemAsync<EventDocument>(
                id.ToString(),
                new PartitionKey(id.ToString()));
            
            return MapToDomain(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    
    public async Task SaveAsync(Event entity)
    {
        var document = MapToDocument(entity);
        
        await _container.UpsertItemAsync(
            document,
            new PartitionKey(document.PartitionKey));
        
        _logger.LogInformation("Saved event: {Id}", entity.Id);
    }
    
    public async Task DeleteAsync(Guid id)
    {
        await _container.DeleteItemAsync<EventDocument>(
            id.ToString(),
            new PartitionKey(id.ToString()));
    }
    
    private Event MapToDomain(EventDocument doc)
    {
        return new Event
        {
            Id = Guid.Parse(doc.Id),
            Name = doc.Name,
            StartDate = doc.StartDate,
            Status = Enum.Parse<EventStatus>(doc.Status)
        };
    }
    
    private EventDocument MapToDocument(Event entity)
    {
        return new EventDocument
        {
            Id = entity.Id.ToString(),
            PartitionKey = entity.Id.ToString(),
            Name = entity.Name,
            StartDate = entity.StartDate,
            Status = entity.Status.ToString()
        };
    }
}
```

## Querying Patterns

### Point Read (Best Performance)
```csharp
// Provides ID and partition key
var entity = await _container.ReadItemAsync<EntityDocument>(
    id.ToString(),
    new PartitionKey(partitionKeyValue));
```

### Query Within Partition
```csharp
var query = _container.GetItemQueryIterator<EntityDocument>(
    new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
        .WithParameter("@status", "Active"),
    requestOptions: new QueryRequestOptions 
    { 
        PartitionKey = new PartitionKey(partitionKeyValue) 
    });
```

### Cross-Partition Query (Expensive)
```csharp
// Avoid when possible, required for queries across all data
var query = _container.GetItemQueryIterator<EntityDocument>(
    "SELECT * FROM c WHERE c.name LIKE 'A%'");
```

## Optimistic Concurrency

### Using ETags
```csharp
public async Task UpdateAsync(Event entity, string etag)
{
    var document = MapToDocument(entity);
    
    var requestOptions = new ItemRequestOptions
    {
        IfMatchEtag = etag
    };
    
    try
    {
        await _container.ReplaceItemAsync(
            document,
            document.Id,
            new PartitionKey(document.PartitionKey),
            requestOptions);
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
    {
        throw new ConcurrencyException("Entity was modified by another process");
    }
}
```

## Change Feed

### Purpose
- React to changes in Cosmos DB
- Publish domain events based on data changes
- Synchronize with other systems
- Audit trail generation

### Implementation
```csharp
var processor = _container.GetChangeFeedProcessorBuilder<EventDocument>(
    "eventProcessor", 
    HandleChangesAsync)
    .WithInstanceName("instance1")
    .WithLeaseContainer(_leaseContainer)
    .Build();

await processor.StartAsync();

async Task HandleChangesAsync(
    ChangeFeedProcessorContext context,
    IReadOnlyCollection<EventDocument> changes,
    CancellationToken cancellationToken)
{
    foreach (var document in changes)
    {
        // Publish integration event
        await _messageSession.Publish(new EntityChangedEvent
        {
            EntityId = Guid.Parse(document.Id),
            ChangeType = "Updated"
        });
    }
}
```

## Data Initialization

### Database Setup
```csharp
public class CosmosDbInitializer
{
    public async Task InitializeAsync(CosmosClient client)
    {
        // Create database
        var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(
            "EventManagementDb",
            throughput: 400);
        
        var database = databaseResponse.Database;
        
        // Create containers
        await database.CreateContainerIfNotExistsAsync(
            new ContainerProperties
            {
                Id = "Events",
                PartitionKeyPath = "/id",
                DefaultTimeToLive = -1 // Enables TTL
            });
        
        await database.CreateContainerIfNotExistsAsync(
            new ContainerProperties
            {
                Id = "Sagas",
                PartitionKeyPath = "/id"
            });
    }
}
```

## Performance Considerations

### Indexing
- Cosmos DB automatically indexes all properties
- Customize indexing policy for better performance
- Exclude large properties not used in queries

### Request Units (RUs)
- Every operation costs RUs
- Monitor and optimize based on workload
- Consider autoscale vs manual throughput

### Batch Operations
```csharp
var batch = _container.CreateTransactionalBatch(new PartitionKey(partitionKeyValue));
batch.CreateItem(document1);
batch.CreateItem(document2);
await batch.ExecuteAsync();
```

## Anti-Patterns to Avoid

❌ **Don't use domain entities as documents** - Separate concerns
❌ **Don't ignore partition key design** - Critical for performance
❌ **Don't do cross-partition queries in hot paths** - Very expensive
❌ **Don't store large documents** - Keep under 2MB
❌ **Don't skip error handling** - Handle CosmosException properly
❌ **Don't forget optimistic concurrency** - Use ETags for updates
❌ **Don't share databases between domains** - Violates bounded context

## Testing

### Unit Tests
```csharp
// Mock repository for domain tests
var mockRepo = new Mock<IEventRepository>();
mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
    .ReturnsAsync(new Event { /* ... */ });
```

### Integration Tests
```csharp
// Use Cosmos DB Emulator for integration tests
var client = new CosmosClient("https://localhost:8081", emulatorKey);
var repo = new EventRepository(client, logger);
await repo.SaveAsync(testEntity);
var retrieved = await repo.GetByIdAsync(testEntity.Id);
Assert.Equal(testEntity.Name, retrieved.Name);
```
