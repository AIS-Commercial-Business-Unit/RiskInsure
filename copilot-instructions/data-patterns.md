# Data Patterns

## Overview

This bounded context uses Azure Cosmos DB as the primary database following the principle of **one database per domain**. This ensures clear data ownership, independent scaling, and bounded context isolation.

## Example Scenario

Throughout this document, we use a **Product Catalog** domain with the following entities:

**Core Entities**:
- **Product**: Main entity (ID, Name, SKU, Price, Description, Status)
- **Category**: Product classification (ID, Name, ParentCategoryId)
- **Inventory**: Stock tracking (ProductId, Quantity, WarehouseLocation)
- **PriceHistory**: Historical price changes (ProductId, OldPrice, NewPrice, EffectiveDate)

**Data Relationships**:
- One Product belongs to one Category
- One Product has one Inventory record per warehouse
- One Product has many PriceHistory records

**Partition Strategy**: Products partitioned by Category for efficient category-based queries

This comprehensive example demonstrates various data patterns applicable across domains.

## Core Principles

### 1. One Database Per Domain
Each bounded context has its own Cosmos DB database:
- Clear data ownership and boundaries
- Independent schema evolution
- Separate scaling and performance tuning
- Isolation of failures

### 2. Repository Pattern
All database access goes through repository interfaces:

### ⚠️ CRITICAL: Repositories Are Persistence-Only

**Repositories handle ONLY data persistence** - they have ZERO business logic.

**What Repositories DO**:
- ✅ CRUD operations (Create, Read, Update, Delete)
- ✅ Query execution (SELECT, WHERE, JOIN)
- ✅ Transaction management
- ✅ Optimistic concurrency handling (ETags)
- ✅ Connection/client management
- ✅ Data mapping (Document ↔ Domain Entity)

**What Repositories DO NOT DO**:
- ❌ Business rule validation
- ❌ Calculations (totals, balances, etc.)
- ❌ State transitions
- ❌ Event publishing
- ❌ Calling other services
- ❌ Workflow orchestration

**Why This Matters**:
- Business logic in repositories violates single responsibility
- Cannot test business rules without database
- Duplicates logic when multiple managers use same repository
- Breaks architectural layering (data layer doing domain work)

**Example - WRONG (Business Logic in Repository)**:
```csharp
// ❌ BAD - Repository doing business logic
public async Task<BillingAccount> RecordPaymentAsync(
    string accountId, decimal amount, string referenceNumber)
{
    var account = await GetByAccountIdAsync(accountId);
    
    // ❌ BUSINESS LOGIC - belongs in Manager!
    account.TotalPaid += amount;
    account.LastUpdatedUtc = DateTimeOffset.UtcNow;
    
    await UpdateAsync(account);
    return account;
}
```

**Example - CORRECT (Pure Persistence)**:
```csharp
// ✅ GOOD - Repository only persists
public async Task UpdateAsync(BillingAccount account)
{
    var document = MapToDocument(account);
    
    await _container.ReplaceItemAsync(
        document,
        document.Id,
        new PartitionKey(account.AccountId),
        new ItemRequestOptions { IfMatchEtag = account.ETag });
}

// ✅ Manager handles business logic
public async Task<PaymentResult> RecordPaymentAsync(RecordPaymentDto dto)
{
    var account = await _repository.GetByAccountIdAsync(dto.AccountId);
    
    // ✅ BUSINESS LOGIC in Manager
    account.TotalPaid += dto.Amount;
    account.LastUpdatedUtc = DateTimeOffset.UtcNow;
    
    // Repository only persists
    await _repository.UpdateAsync(account);
    
    return PaymentResult.Success();
}
```

**Domain defines the contract:**
```csharp
// Domain/Repositories/IProductRepository.cs
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, string categoryId);
    Task<IEnumerable<Product>> GetByCategoryAsync(string categoryId);
    Task SaveAsync(Product product);
    Task DeleteAsync(Guid id, string categoryId);
}
```

**Infrastructure implements:**
```csharp
// Infrastructure/Repositories/ProductRepository.cs
public class ProductRepository : IProductRepository
{
    private readonly Container _container;
    
    public async Task<Product?> GetByIdAsync(Guid id, string categoryId)
    {
        try
        {
            var document = await _container.ReadItemAsync<ProductDocument>(
                id.ToString(), 
                new PartitionKey(categoryId));
            return MapToDomain(document.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
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
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string SKU { get; set; }
    public decimal Price { get; set; }
    public string CategoryId { get; set; }
    public ProductStatus Status { get; set; }
    
    // Rich domain logic
    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice <= 0)
            throw new InvalidOperationException("Price must be positive");
        Price = newPrice;
    }
}
```

**Read Model (Query Side):**
```csharp
public class ProductListItem
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; }
    public decimal Price { get; set; }
    public string CategoryName { get; set; }
    public int AvailableQuantity { get; set; }
    public string StatusDisplay { get; set; }
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
└── ProductCatalogDb
    ├── Products (container) - Main product data
    ├── Categories (container) - Product categories
    ├── Inventory (container) - Stock levels per warehouse
    ├── PriceHistory (container) - Historical pricing
    ├── Sagas (container) - NServiceBus saga data
    └── Leases (container) - Change feed tracking
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
// Example: Product with id "123e4567-e89b-12d3-a456-426614174000"
```
- Simple but limits query performance
- Each partition contains single document
- Use only for high-volume, ID-based lookups

**Strategy 2: Category-Based**
```csharp
PartitionKey = /categoryId
// Example: All "Electronics" products in same partition
```
- Groups related products together
- Efficient for category-based queries
- Enables cross-product queries within category

**Strategy 3: Tenant/Customer ID**
```csharp
PartitionKey = /tenantId
// Example: Multi-tenant SaaS product catalog
```
- Ideal for multi-tenant scenarios
- Natural data isolation per customer
- Scales with number of tenants

**Strategy 4: Composite**
```csharp
PartitionKey = /tenantId-categoryId
// Example: "tenant123-electronics" for tenant-specific categories
```
- Balances distribution and query efficiency
- Combines benefits of multiple strategies
- Better distribution for large tenants

### Choosing a Strategy
- Analyze access patterns
- Consider data volume
- Balance between distribution and query efficiency
- Include partition key in all queries for best performance

## Document Structure

### ⚠️ CRITICAL: Cosmos DB 'id' Field Requirement - READ THIS FIRST

**Every Cosmos DB document MUST have an `id` field with `[JsonPropertyName("id")]` attribute**:

```csharp
using System.Text.Json.Serialization;

public class YourDocument
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }  // REQUIRED by Cosmos DB
    
    // Your other properties...
}
```

**Common Error**:
```
BadRequest (400)
"The input content is invalid because the required properties - 'id; ' - are missing"
```

**Solution**: Add `id` property and set it to your business identifier:
```csharp
public class PaymentMethod
{
    [JsonPropertyName("id")]  // Maps to Cosmos DB 'id' field
    public required string Id { get; set; }
    
    public string PaymentMethodId { get; set; }  // Business identifier
}

// In repository:
var document = new PaymentMethod
{
    Id = paymentMethodId,  // ✅ Set Cosmos DB id
    PaymentMethodId = paymentMethodId  // ✅ Set business identifier
};
```

**Best Practice**: Use same value for both `id` and your business identifier (see BillingAccountRepository example).

### Mapping Domain to Database

### Detailed 'id' Field Pattern

**Every Cosmos DB document MUST have an `id` field**:
- Required by Cosmos DB (will fail with 400 BadRequest if missing)
- Must be unique within the partition
- Combined with partition key, forms the complete unique identifier
- Use `[JsonPropertyName("id")]` attribute in C#

**Best Practice**: Use your business identifier as the Cosmos `id`
- Simplifies lookups (one identifier, not two)
- Example: For billing accounts, use `accountId` as both the business ID and Cosmos `id`
- Example: For products, use product ID as Cosmos `id`

**Domain Entity:**
```csharp
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string SKU { get; set; }
    public decimal Price { get; set; }
    public string CategoryId { get; set; }
    public ProductStatus Status { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastModifiedUtc { get; set; }
}
```

**Database Document:**
```csharp
public class ProductDocument
{
    /// <summary>
    /// Cosmos DB required document identifier.
    /// CRITICAL: Must be set for every document or create/upsert will fail.
    /// </summary>
    [JsonProperty("id")]
    public required string Id { get; set; }  // Use product business ID
    
    /// <summary>
    /// Partition key - determines data distribution.
    /// </summary>
    [JsonProperty("categoryId")]  // Partition key
    public required string CategoryId { get; set; }
    
    [JsonProperty("name")]
    public string Name { get; set; }
    
    [JsonProperty("sku")]
    public string SKU { get; set; }
    
    [JsonProperty("price")]
    public decimal Price { get; set; }
    
    [JsonProperty("status")]
    public string Status { get; set; }
    
    [JsonProperty("createdUtc")]
    public DateTime CreatedUtc { get; set; }
    
    [JsonProperty("lastModifiedUtc")]
    public DateTime? LastModifiedUtc { get; set; }
    
    [JsonProperty("_etag")]
    public string ETag { get; set; }
}
```

### Best Practices
- Use separate document classes (don't reuse domain entities)
- Use `[JsonProperty]` for lowercase, consistent naming
- Store enums as strings for readability
- Include `_etag` for optimistic concurrency
- **ALWAYS set the `id` field** - required by Cosmos DB
- **Use business identifier as `id`** - simplifies lookups and reduces duplication

### The 'id' Field Pattern

**Problem**: Every Cosmos DB document requires an `id` field, but domain models use business identifiers.

**Solution**: Use your business identifier as both the domain ID and Cosmos `id`.

**Example - Billing Account**:
```csharp
// Domain Model
public class BillingAccount
{
    public required string AccountId { get; set; }  // Business identifier
    // ... other properties
}

// Document Model
public class BillingAccountDocument
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }  // Cosmos DB requirement
    
    [JsonPropertyName("accountId")]  // Also store as partition key
    public required string AccountId { get; set; }
    
    // ... other properties
}

// Repository Mapping
private static BillingAccountDocument MapToDocument(BillingAccount account)
{
    return new BillingAccountDocument
    {
        Id = account.AccountId,           // ✅ Set id to business identifier
        AccountId = account.AccountId,    // ✅ Also set partition key field
        // ... map other fields
    };
}
```

**Why Both `id` and `accountId`?**
- `id`: Required by Cosmos DB for document identification
- `accountId`: Used as partition key path (`/accountId`) and in queries
- **Same value**: Eliminates confusion and simplifies lookups

**Common Patterns by Entity Type**:

| Entity | Business ID | Cosmos `id` | Partition Key |
|--------|-------------|-------------|---------------|
| **Billing Account** | `accountId` | `accountId` | `/accountId` |
| **Product** | `productId` | `productId` | `/categoryId` |
| **Order** | `orderId` | `orderId` | `/customerId` |
| **Saga** | `sagaId` | `sagaId` | `/id` |

**⚠️ Common Mistakes**:
```csharp
// ❌ WRONG - id not set
var document = new ProductDocument
{
    ProductId = product.Id,  // Set business ID but forgot Cosmos id
    Name = product.Name
};
await _container.CreateItemAsync(document);  // FAILS with 400 BadRequest

// ✅ CORRECT - id explicitly set
var document = new ProductDocument
{
    Id = product.Id.ToString(),      // ✅ Cosmos DB id
    ProductId = product.Id.ToString(), // ✅ Business ID (if needed)
    Name = product.Name
};
await _container.CreateItemAsync(document);  // SUCCESS
```

## Repository Implementation

### Full Repository Example
```csharp
public class ProductRepository : IProductRepository
{
    private readonly Container _container;
    private readonly ILogger<ProductRepository> _logger;
    
    public ProductRepository(CosmosClient client, ILogger<ProductRepository> logger)
    {
        var database = client.GetDatabase("ProductCatalogDb");
        _container = database.GetContainer("Products");
        _logger = logger;
    }
    
    public async Task<Product?> GetByIdAsync(Guid id, string categoryId)
    {
        try
        {
            var response = await _container.ReadItemAsync<ProductDocument>(
                id.ToString(),
                new PartitionKey(categoryId));
            
            return MapToDomain(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    
    public async Task<IEnumerable<Product>> GetByCategoryAsync(string categoryId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.categoryId = @categoryId")
            .WithParameter("@categoryId", categoryId);
        
        var iterator = _container.GetItemQueryIterator<ProductDocument>(
            query,
            requestOptions: new QueryRequestOptions 
            { 
                PartitionKey = new PartitionKey(categoryId) 
            });
        
        var products = new List<Product>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            products.AddRange(response.Select(MapToDomain));
        }
        
        return products;
    }
    
    public async Task SaveAsync(Product product)
    {
        var document = MapToDocument(product);
        
        await _container.UpsertItemAsync(
            document,
            new PartitionKey(document.CategoryId));
        
        _logger.LogInformation("Saved product: {SKU} in category {CategoryId}", 
            product.SKU, product.CategoryId);
    }
    
    public async Task DeleteAsync(Guid id, string categoryId)
    {
        await _container.DeleteItemAsync<ProductDocument>(
            id.ToString(),
            new PartitionKey(categoryId));
        
        _logger.LogInformation("Deleted product: {ProductId}", id);
    }
    
    private Product MapToDomain(ProductDocument doc)
    {
        return new Product
        {
            Id = Guid.Parse(doc.Id),
            Name = doc.Name,
            SKU = doc.SKU,
            Price = doc.Price,
            CategoryId = doc.CategoryId,
            Status = Enum.Parse<ProductStatus>(doc.Status),
            CreatedUtc = doc.CreatedUtc,
            LastModifiedUtc = doc.LastModifiedUtc
        };
    }
    
    private ProductDocument MapToDocument(Product product)
    {
        return new ProductDocument
        {
            Id = product.Id.ToString(),
            Name = product.Name,
            SKU = product.SKU,
            Price = product.Price,
            CategoryId = product.CategoryId,
            Status = product.Status.ToString(),
            CreatedUtc = product.CreatedUtc,
            LastModifiedUtc = product.LastModifiedUtc ?? DateTime.UtcNow
        };
    }
}
```

## Querying Patterns

### Point Read (Best Performance)
```csharp
// Provides ID and partition key (categoryId)
var product = await _container.ReadItemAsync<ProductDocument>(
    productId.ToString(),
    new PartitionKey(categoryId));
```

### Query Within Partition
```csharp
// Get all active products in Electronics category
var query = _container.GetItemQueryIterator<ProductDocument>(
    new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
        .WithParameter("@status", "Active"),
    requestOptions: new QueryRequestOptions 
    { 
        PartitionKey = new PartitionKey("electronics") 
    });
```

### Cross-Partition Query (Expensive)
```csharp
// Avoid when possible - searches across all categories
var query = _container.GetItemQueryIterator<ProductDocument>(
    new QueryDefinition(
        "SELECT * FROM c WHERE c.sku = @sku")
        .WithParameter("@sku", "PROD-12345"));
```

### Aggregate Query Within Partition
```csharp
// Get average price for products in a category
var query = _container.GetItemQueryIterator<dynamic>(
    new QueryDefinition(
        "SELECT AVG(c.price) as avgPrice FROM c")
    requestOptions: new QueryRequestOptions 
    { 
        PartitionKey = new PartitionKey(categoryId) 
    });
```

## Optimistic Concurrency

### Using ETags
```csharp
public async Task UpdatePriceAsync(Guid productId, string categoryId, decimal newPrice, string etag)
{
    var product = await GetByIdAsync(productId, categoryId);
    if (product == null)
        throw new NotFoundException($"Product {productId} not found");
    
    product.UpdatePrice(newPrice);
    product.LastModifiedUtc = DateTime.UtcNow;
    
    var document = MapToDocument(product);
    
    var requestOptions = new ItemRequestOptions
    {
        IfMatchEtag = etag
    };
    
    try
    {
        await _container.ReplaceItemAsync(
            document,
            document.Id,
            new PartitionKey(document.CategoryId),
            requestOptions);
        
        _logger.LogInformation(
            "Updated price for product {SKU} from {OldPrice} to {NewPrice}",
            product.SKU, product.Price, newPrice);
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
    {
        throw new ConcurrencyException(
            $"Product {productId} was modified by another process. Please retry.");
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
var processor = _container.GetChangeFeedProcessorBuilder<ProductDocument>(
    "productPriceChangeProcessor", 
    HandlePriceChangesAsync)
    .WithInstanceName("instance1")
    .WithLeaseContainer(_leaseContainer)
    .WithStartTime(DateTime.UtcNow.AddHours(-1)) // Process last hour
    .Build();

await processor.StartAsync();

async Task HandlePriceChangesAsync(
    ChangeFeedProcessorContext context,
    IReadOnlyCollection<ProductDocument> changes,
    CancellationToken cancellationToken)
{
    foreach (var document in changes)
    {
        // Track price changes for analytics
        if (HasPriceChanged(document))
        {
            await _messageSession.Publish(new PriceChanged
            {
                MessageId = Guid.NewGuid(),
                OccurredUtc = DateTimeOffset.UtcNow,
                ProductId = Guid.Parse(document.Id),
                SKU = document.SKU,
                NewPrice = document.Price,
                CategoryId = document.CategoryId,
                IdempotencyKey = $"price-changed-{document.Id}-{document.LastModifiedUtc:yyyyMMddHHmmss}"
            });
            
            _logger.LogInformation(
                "Price changed for product {SKU} to {NewPrice}",
                document.SKU, document.Price);
        }
    }
}
```

## Data Initialization

### ⚠️ CRITICAL: Configure Cosmos DB Serialization

**Problem**: Cosmos DB SDK defaults to **Newtonsoft.Json**, but .NET uses **System.Text.Json**. Using `[JsonPropertyName]` attributes without proper serializer configuration causes fields to be ignored.

**Symptoms**:
- 400 BadRequest: "required properties - 'id; ' - are missing"
- Document fields missing even though properties are set
- Serialization attributes ignored

**Solution**: Configure CosmosClient to use System.Text.Json serializer.

### Step 1: Create Custom Serializer (Infrastructure Project)

```csharp
// Infrastructure/CosmosSystemTextJsonSerializer.cs
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Cosmos;

/// <summary>
/// Custom Cosmos DB serializer using System.Text.Json instead of Newtonsoft.Json.
/// Required to properly serialize documents with [JsonPropertyName] attributes.
/// </summary>
public class CosmosSystemTextJsonSerializer : CosmosSerializer
{
    private readonly JsonSerializerOptions _options;

    public CosmosSystemTextJsonSerializer(JsonSerializerOptions options)
    {
        _options = options;
    }

    public override T FromStream<T>(Stream stream)
    {
        if (stream == null || stream.Length == 0)
        {
            return default!;
        }

        using (stream)
        {
            return JsonSerializer.Deserialize<T>(stream, _options)!;
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var json = JsonSerializer.Serialize(input, _options);
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }
}
```

### Step 2: Configure CosmosClient with Custom Serializer

```csharp
// API/Program.cs or Endpoint/Program.cs
var cosmosConnectionString = builder.Configuration["CosmosDb:ConnectionString"];

// ✅ CORRECT - Configure serializer
var cosmosClientOptions = new CosmosClientOptions
{
    Serializer = new CosmosSystemTextJsonSerializer(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    })
};

var cosmosClient = new CosmosClient(cosmosConnectionString, cosmosClientOptions);

// ❌ WRONG - Uses Newtonsoft.Json by default
var cosmosClient = new CosmosClient(cosmosConnectionString);
```

**Why This Matters**:
- Without custom serializer: `[JsonPropertyName("id")]` is ignored → Cosmos DB sees missing 'id' field
- With custom serializer: `[JsonPropertyName("id")]` properly maps `Id` property to JSON "id" field

### ⚠️ CRITICAL: Initialize Database and Containers on Startup

**Problem**: Attempting to read/write to a non-existent container will fail with `NotFound (404)`.

**Solution**: **ALWAYS** initialize database and containers **BEFORE** getting container references.

### Infrastructure CosmosDbInitializer (Shared Utility)

Create this in `Infrastructure` project for reuse across API and Endpoint projects:

```csharp
// Infrastructure/CosmosDbInitializer.cs
using Microsoft.Azure.Cosmos;

namespace Infrastructure
{
    /// <summary>
    /// Initializes Cosmos DB database and containers on application startup.
    /// Ensures all required resources exist before the application processes requests.
    /// </summary>
    public static class CosmosDbInitializer
    {
        /// <summary>
        /// Creates database and container if they don't exist.
        /// </summary>
        /// <param name="client">Cosmos DB client</param>
        /// <param name="dbName">Database name</param>
        /// <param name="containerName">Container name</param>
        /// <param name="partitionKeyPath">Partition key path (e.g., "/accountId", "/categoryId")</param>
        /// <param name="throughput">Optional throughput (RU/s). Default is 400 RU/s.</param>
        public static async Task EnsureDbAndContainerAsync(
            CosmosClient client, 
            string dbName, 
            string containerName, 
            string partitionKeyPath,
            int? throughput = 400)
        {
            // Create database if it doesn't exist
            var dbResponse = await client.CreateDatabaseIfNotExistsAsync(
                dbName, 
                throughput: throughput);
            
            // Create container if it doesn't exist
            await dbResponse.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties
                {
                    Id = containerName,
                    PartitionKeyPath = partitionKeyPath,
                    DefaultTimeToLive = -1 // Enable TTL but don't auto-delete
                });
        }
    }
}
```

### API/Endpoint Program.cs Integration (REQUIRED PATTERN)

**CRITICAL**: Call initialization **BEFORE** getting container reference.

```csharp
// API/Program.cs or Endpoint/Program.cs
using Infrastructure;
using Microsoft.Azure.Cosmos;

// ... logger setup ...

try
{
    Log.Information("Starting Product Catalog API");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Cosmos DB
    var cosmosConnectionString = builder.Configuration.GetConnectionString("CosmosDb")
        ?? throw new InvalidOperationException("CosmosDb connection string not configured");

    var databaseName = builder.Configuration["CosmosDb:DatabaseName"] ?? "ProductCatalogDb";
    var containerName = builder.Configuration["CosmosDb:ProductsContainerName"] ?? "Products";

    var cosmosClient = new CosmosClient(cosmosConnectionString);
    
    // ✅ CRITICAL: Initialize BEFORE getting container
    Log.Information("Initializing Cosmos DB database {DatabaseName} and container {ContainerName}", 
        databaseName, containerName);
    await CosmosDbInitializer.EnsureDbAndContainerAsync(
        cosmosClient, 
        databaseName, 
        containerName, 
        "/categoryId"); // Partition key path - MUST match your document property
    
    // ✅ NOW safe to get container reference
    var container = cosmosClient.GetContainer(databaseName, containerName);
    builder.Services.AddSingleton(container);

    // Register repositories
    builder.Services.AddSingleton<IProductRepository, ProductRepository>();
    
    // ... rest of configuration ...
}
```

### appsettings Configuration

```json
{
  "ConnectionStrings": {
    "CosmosDb": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
  },
  "CosmosDb": {
    "DatabaseName": "ProductCatalogDb",
    "ProductsContainerName": "Products"
  }
}
```

### Advanced Initialization (Multiple Containers)

For complex domains with multiple containers:

```csharp
public class CosmosDbInitializer
{
    public async Task InitializeAsync(CosmosClient client)
    {
        // Create database
        var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(
            "ProductCatalogDb",
            throughput: 400);
        
        var database = databaseResponse.Database;
        
        // Create Products container (partitioned by categoryId)
        await database.CreateContainerIfNotExistsAsync(
            new ContainerProperties
            {
                Id = "Products",
                PartitionKeyPath = "/categoryId",
                DefaultTimeToLive = -1, // Enables TTL
                IndexingPolicy = new IndexingPolicy
                {
                    IndexingMode = IndexingMode.Consistent,
                    IncludedPaths = new Collection<IncludedPath>
                    {
                        new IncludedPath { Path = "/*" }
                    },
                    ExcludedPaths = new Collection<ExcludedPath>
                    {
                        new ExcludedPath { Path = "/description/*" } // Large text field
                    }
                }
            });
        
        // Create Categories container
        await database.CreateContainerIfNotExistsAsync(
            new ContainerProperties
            {
                Id = "Categories",
                PartitionKeyPath = "/id"
            });
        
        // Create Inventory container (partitioned by productId)
        await database.CreateContainerIfNotExistsAsync(
            new ContainerProperties
            {
                Id = "Inventory",
                PartitionKeyPath = "/productId"
            });
        
        // Create PriceHistory container (partitioned by productId)
        await database.CreateContainerIfNotExistsAsync(
            new ContainerProperties
            {
                Id = "PriceHistory",
                PartitionKeyPath = "/productId",
                DefaultTimeToLive = 2592000 // 30 days
            });
        
        // Create Sagas container for NServiceBus
        await database.CreateContainerIfNotExistsAsync(
            new ContainerProperties
            {
                Id = "Sagas",
                PartitionKeyPath = "/id"
            });
    }
}
```

### Common Partition Key Patterns by Domain

| Domain | Partition Key | Rationale |
|--------|---------------|-----------|
| **Billing Accounts** | `/accountId` | Independent account lifecycle, direct lookups |
| **Orders** | `/customerId` | Query all customer orders together |
| **Products** | `/categoryId` | Category-based browsing and filtering |
| **Inventory** | `/warehouseId` | Warehouse-specific stock queries |
| **Sagas** | `/id` | Each saga instance independent |
| **Multi-Tenant** | `/tenantId` | Natural tenant isolation |

### Troubleshooting

**Error: "Resource Not Found (404)"**
- **Cause**: Container doesn't exist
- **Fix**: Ensure `CosmosDbInitializer.EnsureDbAndContainerAsync()` called before `GetContainer()`

**Error: "Partition key mismatch"**
- **Cause**: Document missing partition key property or wrong path
- **Fix**: Ensure document has property matching partition key path (e.g., document has `accountId` for `/accountId`)

**Error: "Invalid partition key"**
- **Cause**: Partition key path doesn't start with `/` or references nested property incorrectly
- **Fix**: Use `/propertyName` format, not `propertyName`

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
// Create multiple products in same category atomically
var categoryId = "electronics";
var batch = _container.CreateTransactionalBatch(new PartitionKey(categoryId));

var product1 = new ProductDocument { Id = Guid.NewGuid().ToString(), CategoryId = categoryId, Name = "Laptop", Price = 999.99m };
var product2 = new ProductDocument { Id = Guid.NewGuid().ToString(), CategoryId = categoryId, Name = "Mouse", Price = 29.99m };

batch.CreateItem(product1);
batch.CreateItem(product2);

var response = await batch.ExecuteAsync();
if (!response.IsSuccessStatusCode)
{
    _logger.LogError("Batch operation failed: {StatusCode}", response.StatusCode);
}
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
var mockRepo = new Mock<IProductRepository>();
mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<string>()))
    .ReturnsAsync(new Product 
    { 
        Id = Guid.NewGuid(),
        Name = "Test Product",
        SKU = "TEST-001",
        Price = 99.99m,
        CategoryId = "electronics",
        Status = ProductStatus.Active
    });

var service = new ProductService(mockRepo.Object);
var result = await service.UpdatePriceAsync(productId, categoryId, 109.99m);
Assert.True(result.IsSuccess);
```

### Integration Tests
```csharp
// Use Cosmos DB Emulator for integration tests
var client = new CosmosClient("https://localhost:8081", emulatorKey);
var repo = new ProductRepository(client, logger);

var testProduct = new Product
{
    Id = Guid.NewGuid(),
    Name = "Integration Test Product",
    SKU = "IT-001",
    Price = 49.99m,
    CategoryId = "test-category",
    Status = ProductStatus.Active,
    CreatedUtc = DateTime.UtcNow
};

await repo.SaveAsync(testProduct);
var retrieved = await repo.GetByIdAsync(testProduct.Id, testProduct.CategoryId);

Assert.NotNull(retrieved);
Assert.Equal(testProduct.Name, retrieved.Name);
Assert.Equal(testProduct.SKU, retrieved.SKU);
Assert.Equal(testProduct.Price, retrieved.Price);
```
