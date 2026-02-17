# Manager Architecture Pattern: The Constitution

## Table of Contents

1. [Overview](#overview)
2. [Implementation Guidelines](#implementation-guidelines)
3. [Architectural Principles](#architectural-principles)
4. [The Three Layers](#the-three-layers)
5. [Validation vs Business Rules](#validation-vs-business-rules)
6. [Manager Responsibilities](#manager-responsibilities)
7. [Protocol Translation Pattern](#protocol-translation-pattern)
8. [Event Publishing Patterns](#event-publishing-patterns)
9. [Service Dependencies](#service-dependencies)
10. [Product Catalog Example](#product-catalog-example)
11. [Dependency Injection Wiring](#dependency-injection-wiring)
12. [Sequence Diagrams](#sequence-diagrams)
13. [Common Patterns](#common-patterns)

---

## Overview

This document defines the architectural pattern for domain managers that serve as the **surface area** for domain operations. Managers encapsulate business logic and are called by two types of external interfaces:

1. **APIs** (HTTP protocol) - Synchronous request/response
2. **Event Handlers** (AMQP protocol) - Asynchronous message processing

Both the API and event handlers perform the same translation responsibility: receiving data via their respective protocols, validating structural correctness, and delegating to the Manager for business logic execution.

### Core Philosophy

> **Managers own business logic. APIs and Handlers own protocol translation.**

```
HTTP Request          AMQP Message
    ↓                      ↓
 [API Handler]        [Event Handler]
    ↓                      ↓
 Validate DTO         Validate DTO
    ↓                      ↓
 Call Manager      Call Manager
    ↓                      ↓
 [Business Logic Surface]
```

---

## Implementation Guidelines

### Always Ask Clarifying Questions First

**CRITICAL**: Before implementing Manager patterns for a new domain, always ask clarifying questions to understand:

1. **Aggregate Root Identification**
   - What is the primary aggregate root for this Manager?
   - Example: "Is the Billing aggregate root `BillingAccount`, `Payment`, `Invoice`, or something else?"

2. **Business Capabilities Discovery**
   - What are the actual business operations this domain performs?
   - Example: "What business capabilities should the Billing Manager expose? (e.g., `ProcessPayment()`, `RecordPaymentForAccount()`, etc.)"

3. **API Pattern Confirmation**
   - Should the API support synchronous operations (direct manager call, 200 response)?
   - Should the API support asynchronous operations (publish command, 202 response)?
   - Or both patterns?
   - Example: "Should the BillingController support synchronous payment recording or only async via commands?"

4. **Business Rules Clarification**
   - What business rules exist for this domain?
   - What validation should be enforced?
   - What constraints must be checked?
   - Example: "What business rules exist for payment recording? Can payments exceed outstanding balance? Are negative payments allowed?"

5. **Documentation Scope**
   - Should this implementation be documented as an example in manager-and-facade.md?
   - Or should the pattern be applied without updating the documentation?
   - Example: "Should I add a Billing example to manager-and-facade.md or just implement it?"

### Example Question Template

When asked to implement Manager patterns, use this template:

```markdown
## Questions Before Implementation:

1. **Aggregate Root**: What is the primary entity this Manager operates on?
   
2. **Business Capabilities**: What are the specific business operations needed?
   (Not CRUD - actual business language operations)

3. **API Pattern**: Should we support:
   - [ ] Synchronous endpoint (200 OK with result)
   - [ ] Asynchronous endpoint (202 Accepted with command)
   - [ ] Both patterns

4. **Business Rules**: What validation/business rules exist?
   (I can use common-sense defaults if unknown)

5. **Documentation**: Should I update manager-and-facade.md with this example?

**Please answer these questions so I can implement the correct solution!**
```

### Why This Matters

- **Prevents rework**: Understanding requirements upfront avoids implementing wrong patterns
- **Domain alignment**: Ensures Manager reflects actual business language and needs
- **Complete implementation**: All necessary pieces (DTOs, results, handlers, registration) are included
- **Correct patterns**: Synchronous vs asynchronous endpoints are implemented as needed
- **Realistic examples**: Business rules reflect actual domain constraints

---

## Architectural Principles

### 1. Single Aggregate Root per Manager

Each Manager operates on a single aggregate root, ensuring clear boundaries and independent scaling.

- ❌ **DO NOT**: Create "ProductCatalogManager" that manages Products, Categories, and Inventory together
- ✅ **DO**: Create "ProductManager", "CategoryManager", "InventoryManager" separately

### 2. Business Capabilities, Not CRUD

Managers expose business capabilities that reflect domain language, not database operations.

- ❌ **Bad**: `Create()`, `Update()`, `Delete()`, `Read()`
- ✅ **Good**: `PromoteProductToFeatured()`, `AdjustPricing()`, `ReserveInventory()`

### 3. Discrete Transactions

Each Manager function is a discrete, independent transaction.

- ❌ **DO NOT**: Implement Unit of Work patterns in Managers
- ✅ **DO**: Use Sagas or Process Managers for multi-step orchestration
- ✅ **DO**: Make multiple Manager calls from a Saga, not from within a Manager

### 4. Protocol Translation Responsibility

APIs and Event Handlers translate from their protocol to C#, then delegate to Managers.

- ❌ **DO NOT**: Put protocol-specific code (HTTP headers, AMQP properties) in Managers
- ✅ **DO**: Put protocol-specific code in APIs and Handlers
- ✅ **DO**: Keep Managers pure C# and business logic

### 5. Data Validation at the Edge

DTO validation catches structural issues before reaching the Manager.

- ❌ **DO NOT**: Put DTO validation in Managers
- ✅ **DO**: Validate DTOs in APIs and Handlers
- ✅ **DO**: Only pass valid DTOs to Managers

---

## The Three Layers

```
┌─────────────────────────────────────────────────────────────┐
│                   PROTOCOL TRANSLATION LAYER                │
│  [HTTP API] ───────────────────────────── [Event Handler]   │
│    • Parse requests/messages                                │
│    • Validate DTOs (structural)                             │
│    • Map to domain DTOs                                     │
│    • Handle HTTP/AMQP specifics                             │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ↓ Manager call
┌──────────────────────────────────────────────────────────────┐
│                    MANAGER LAYER (Domain Logic)              │
│  [ProductManager] / [BeneficiaryManager] / [InventoryMgr]    │
│    • Business rule validation                                │
│    • Coordinate service dependencies                         │
│    • Publish domain events                                   │
│    • Return result objects                                   │
└──────────────────────┬───────────────────────────────────────┘
                       │
           ┌───────────┼───────────┬──────────────┐
           ↓           ↓           ↓              ↓
    ┌──────────────┬──────────────┬──────────────┬─────────────┐
    │  Repository  │ External API │ Domain Svc   │ Message Bus │
    │  (CosmosDB)  │ (AI/Payment) │ (Category)   │ (Publish)   │
    └──────────────┴──────────────┴──────────────┴─────────────┘
```

### Layer 1: Protocol Translation (API & Handlers)

**Responsibility**: Convert from protocol to C# and call Manager

**Examples**:
```csharp
// API - HTTP Protocol Translation
public async Task<HttpResponseData> RegisterBeneficiary(HttpRequestData req)
{
    // 1. Parse HTTP request body
    var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    
    // 2. Deserialize to DTO (validates structure)
    var registrationRequest = JsonSerializer.Deserialize<BeneficiaryRegistrationRequest>(requestBody);
    
    // 3. Validate DTO (catches structural errors early)
    var validationResults = registrationRequest.Validate();
    if (!validationResults.IsValid) return BadRequest(validationResults.Errors);
    
    // 4. Map to domain DTO
    var registrationDto = MapRequestToDto(registrationRequest);
    
    // 5. Call Manager (business logic)
    var result = await _beneficiaryManager.RegisterBeneficiaryAsync(registrationDto);
    
    // 6. Return HTTP response
    return req.CreateResponse(result.IsSuccess ? OK : BadRequest);
}

// Handler - AMQP Protocol Translation
public async Task Handle(CreateBeneficiaryCommand command, IMessageHandlerContext context)
{
    // 1. Message already deserialized by NServiceBus
    
    // 2. Validate message structure (if needed)
    var validationResults = command.Validate();
    if (!validationResults.IsValid) 
    {
        // Publish failure event
        await context.Publish(new BeneficiaryCreationFailed { ... });
        return;
    }
    
    // 3. Map to domain DTO
    var registrationDto = MapCommandToDto(command);
    
    // 4. Call Manager (business logic)
    var result = await _beneficiaryManager.RegisterBeneficiaryAsync(registrationDto);
    
    // 5. Handle response - publish appropriate event
    if (result.IsSuccess)
        await context.Publish(new BeneficiaryCreationSuccess { ... });
    else
        await context.Publish(new BeneficiaryCreationFailed { ... });
}
```

### Layer 2: Manager (Domain Logic)

**Responsibility**: Business logic, service orchestration, event publishing

**Characteristics**:
- Pure C# (no protocol-specific code)
- Validates business rules (not structural validation)
- Coordinates multiple service dependencies
- Publishes domain events
- Returns result objects for complex operations

### Layer 3: Service Dependencies

**Examples**:
- **Repository** (CosmosDB, SQL, etc.) - Data persistence
- **External APIs** (Payment processor, AI service, etc.) - 3rd party logic
- **Domain Services** (CategoryService, ValidationService) - Shared business logic
- **Message Bus** (NServiceBus over RabbitMQ) - Event publishing

---

## Validation vs Business Rules

### Structural Validation (DTO Layer)

**When**: At the protocol translation layer (API & Handlers)

**What**: Check data format, required fields, length constraints, format correctness

**Tools**: Data annotations in DTOs

**Examples**:
```csharp
public class BeneficiaryRegistrationRequest
{
    [Required(ErrorMessage = "First name required")]
    [StringLength(100)]
    public string FirstName { get; set; }
    
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string? Email { get; set; }
    
    [RegularExpression("^\\d{4}-\\d{2}-\\d{2}$", ErrorMessage = "Date must be YYYY-MM-DD")]
    public string DateOfBirth { get; set; }
}
```

**Result**: Only valid DTOs reach the Manager

### Business Rule Validation (Manager Layer)

**When**: Inside Manager functions

**What**: Check business logic constraints, domain invariants, consistency rules

**Why here**: Business rules need data context to validate

**Examples**:
```csharp
public async Task<ProductPricingResult> AdjustProductPricingAsync(
    AdjustPricingDto adjustmentDto)
{
    // Business Rule: New price must be within min/max bounds
    var product = await _repository.GetProductAsync(adjustmentDto.ProductId);
    if (adjustmentDto.NewPrice < product.MinPrice || 
        adjustmentDto.NewPrice > product.MaxPrice)
    {
        return new ProductPricingResult 
        { 
            IsSuccess = false, 
            ErrorMessage = "Price outside allowed bounds" 
        };
    }
    
    // Business Rule: Cannot reduce price more than 30% from current
    var maxReduction = product.CurrentPrice * 0.30m;
    if (product.CurrentPrice - adjustmentDto.NewPrice > maxReduction)
    {
        return new ProductPricingResult 
        { 
            IsSuccess = false, 
            ErrorMessage = "Price reduction exceeds 30% limit" 
        };
    }
    
    // All business rules passed - proceed with update
    await UpdatePricingAsync(adjustmentDto);
    return new ProductPricingResult { IsSuccess = true };
}
```

**Key Distinction**:
```
Structural Validation: "Is this valid C#?"
Business Rule Validation: "Is this valid for our business?"
```

---

## Manager Responsibilities

### Primary Responsibilities

1. **Validate Business Rules** - Ensure data conforms to domain constraints
2. **Coordinate Services** - Call repositories, external APIs, domain services as needed
3. **Publish Domain Events** - Signal that important domain events occurred
4. **Return Results** - For complex operations, return result objects

### What Managers Do NOT Do

- ❌ Receive HTTP requests or AMQP messages
- ❌ Validate structural/format concerns (that's DTOs)
- ❌ Parse JSON or deserialize protocol data
- ❌ Handle multiple aggregate roots in one call
- ❌ Implement Unit of Work patterns across repositories
- ❌ Create saga orchestrations (that's Sagas, not Managers)

### ⚠️ CRITICAL: Business Logic Belongs in Managers, NOT Repositories

**Problem**: It's tempting to put business logic in repositories because they already have data access.

**Why This Is Wrong**:
1. **Violates Single Responsibility**: Repositories should only persist data
2. **Untestable**: Cannot test business rules without database
3. **Duplicates Logic**: Multiple managers can't reuse repository "business methods"
4. **Breaks Layering**: Data layer doing domain work

**Example - WRONG**:
```csharp
// ❌ Repository doing business logic
public class BillingAccountRepository
{
    public async Task<BillingAccount> RecordPaymentAsync(
        string accountId, decimal amount, string referenceNumber)
    {
        var account = await GetByAccountIdAsync(accountId);
        
        // ❌ BUSINESS LOGIC - belongs in Manager!
        if (amount <= 0) throw new Exception("Invalid amount");
        if (account.Status != Active) throw new Exception("Account not active");
        if (amount > account.Outstanding) throw new Exception("Overpayment");
        
        account.TotalPaid += amount;
        account.LastUpdatedUtc = DateTimeOffset.UtcNow;
        
        await UpdateAsync(account);
        return account;
    }
}

// ❌ Manager just passes through to repository
public class BillingPaymentManager
{
    public async Task<PaymentResult> RecordPaymentAsync(RecordPaymentDto dto)
    {
        // ❌ No business logic here - just a pass-through!
        var account = await _repository.RecordPaymentAsync(
            dto.AccountId, dto.Amount, dto.ReferenceNumber);
        return PaymentResult.Success(account);
    }
}
```

**Example - CORRECT**:
```csharp
// ✅ Repository only does CRUD
public class BillingAccountRepository
{
    public async Task<BillingAccount?> GetByAccountIdAsync(string accountId)
    {
        return await _container.ReadItemAsync<BillingAccountDocument>(
            accountId, new PartitionKey(accountId));
    }
    
    public async Task UpdateAsync(BillingAccount account)
    {
        var document = MapToDocument(account);
        await _container.ReplaceItemAsync(
            document, document.Id, new PartitionKey(account.AccountId),
            new ItemRequestOptions { IfMatchEtag = account.ETag });
    }
}

// ✅ Manager contains ALL business logic
public class BillingPaymentManager
{
    public async Task<PaymentResult> RecordPaymentAsync(RecordPaymentDto dto)
    {
        // ✅ Business Rule: Amount validation
        if (dto.Amount <= 0)
            return PaymentResult.Failure("Amount must be positive");
        
        // ✅ Get data via repository (pure read)
        var account = await _repository.GetByAccountIdAsync(dto.AccountId);
        if (account == null)
            return PaymentResult.Failure("Account not found");
        
        // ✅ Business Rule: Status validation
        if (account.Status != BillingAccountStatus.Active)
            return PaymentResult.Failure("Account not active");
        
        // ✅ Business Rule: Overpayment check
        if (dto.Amount > account.OutstandingBalance)
            return PaymentResult.Failure("Payment exceeds balance");
        
        // ✅ BUSINESS LOGIC: Apply payment
        account.TotalPaid += dto.Amount;
        account.LastUpdatedUtc = DateTimeOffset.UtcNow;
        
        // ✅ Persist via repository (pure write)
        await _repository.UpdateAsync(account);
        
        // ✅ Publish domain event
        await _messageSession.Publish(new PaymentReceived(...));
        
        return PaymentResult.Success(account);
    }
}
```

**Key Principle**: 
```
Managers = Business Logic + Orchestration
Repositories = CRUD + Queries + Persistence
```

### Function Naming Convention

Managers expose business capabilities, not CRUD operations:

| ❌ Bad | ✅ Good | Domain Meaning |
|--------|---------|----------------|
| `CreateProduct()` | `RegisterNewProduct()` | Product enters system |
| `UpdatePrice()` | `AdjustProductPricing()` | Pricing strategy change |
| `DeleteInventory()` | `DepletInventoryForShipment()` | Inventory consumed |
| `Read()` | N/A | Managers aren't data retrieval layers |

---

## Protocol Translation Pattern

### The Translation Contract

Every API and Handler must follow this pattern:

```
1. Receive data (HTTP body / AMQP message)
   ↓
2. Deserialize to DTO (framework-specific, e.g., JsonSerializer, NServiceBus)
   ↓
3. Validate DTO (data annotations, validation framework)
   ↓
4. Map DTO to Domain DTO (if different types)
   ↓
5. Call Manager with Domain DTO
   ↓
6. Handle Manager result
   ↓
7. Return protocol-specific response (HTTP status / AMQP event)
```

### Multiple Callers Pattern

The Manager can be called from multiple sources. They all follow the same contract:

```csharp
public class ProductApi
{
    private readonly IProductManager _productManager;
    
    public async Task<HttpResponseData> PromoteToFeatured(HttpRequestData req)
    {
        var request = JsonSerializer.Deserialize<PromoteRequest>(await req.Body);
        var request.ValidateOrThrow(); // Step 3: Validate DTO
        
        var dto = new PromoteProductDto { ... }; // Step 4: Map
        var result = await _productManager.PromoteToFeaturedAsync(dto); // Step 5: Call
        
        return result.IsSuccess ? req.CreateResponse(OK) : req.CreateResponse(BadRequest);
    }
}

public class PromoteProductHandler : IHandleMessages<PromoteProductCommand>
{
    private readonly IProductManager _productManager;
    
    public async Task Handle(PromoteProductCommand command, IMessageHandlerContext context)
    {
        // Step 3: Message handler framework already validated basic structure
        
        var dto = new PromoteProductDto { ... }; // Step 4: Map
        var result = await _productManager.PromoteToFeaturedAsync(dto); // Step 5: Call
        
        // Step 7: Publish event
        if (result.IsSuccess)
            await context.Publish(new ProductPromoted { ... });
    }
}
```

Both call the **same** Manager function with the **same** DTO, proving the Manager is truly protocol-agnostic.

---

## Event Publishing Patterns

Managers publish domain events to signal that important business occurrences happened. Two patterns exist:

### Pattern 1: Direct Event Publishing (Simple)

For straightforward cases where the Manager directly decides what events to publish:

```csharp
public async Task<PromotionResult> PromoteToFeaturedAsync(PromoteProductDto dto)
{
    // Validate business rules
    var product = await _repository.GetProductAsync(dto.ProductId);
    if (product.IsAlreadyFeatured)
        return new PromotionResult { IsSuccess = false, Error = "Already featured" };
    
    // Update product
    product.PromoteToFeatured(dto.FeaturedUntil);
    await _repository.SaveAsync(product);
    
    // DIRECTLY publish event
    var @event = new ProductPromoted
    {
        ProductId = product.Id,
        PromotedAt = DateTimeOffset.UtcNow,
        FeaturedUntil = dto.FeaturedUntil
    };
    
    await _eventPublisher.PublishAsync(@event);
    
    return new PromotionResult { IsSuccess = true };
}
```

### Pattern 2: Domain Model Event Collection (Complex)

For complex cases where a domain model encapsulates business logic and creates events:

```csharp
// Domain Model encapsulates business logic
public class Product
{
    private readonly List<DomainEvent> _domainEvents = new();
    
    public void PromoteToFeatured(DateTimeOffset featuredUntil)
    {
        // Business logic embedded in domain model
        if (this.IsAlreadyFeatured)
            throw new DomainException("Already featured");
        
        this.FeaturedStatus = FeaturedStatus.Featured;
        this.FeaturedUntil = featuredUntil;
        
        // Domain model DECIDES what happened
        _domainEvents.Add(new ProductPromoted 
        { 
            ProductId = this.Id,
            FeaturedUntil = featuredUntil 
        });
    }
    
    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
}

// Manager orchestrates and publishes
public async Task<PromotionResult> PromoteToFeaturedAsync(PromoteProductDto dto)
{
    // Get domain model
    var product = await _repository.GetProductAsync(dto.ProductId);
    
    // Domain model decides what events to create
    product.PromoteToFeatured(dto.FeaturedUntil);
    
    // Manager saves
    await _repository.SaveAsync(product);
    
    // Manager publishes events that domain model created
    foreach (var @event in product.GetDomainEvents())
    {
        await _eventPublisher.PublishAsync(@event);
    }
    
    return new PromotionResult { IsSuccess = true };
}
```

**Pattern 2 is preferred** because it keeps business logic in the domain model, not the Manager, but Pattern 1 is acceptable for simpler scenarios.

---

## Service Dependencies

Managers can depend on multiple types of services. Inject them via constructor:

```csharp
public class ProductManager : IProductManager
{
    private readonly IProductRepository _repository;
    private readonly ICategoryService _categoryService;
    private readonly IPricingApi _pricingApi;
    private readonly IAiRecommendationService _aiService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ProductManager> _logger;
    
    public ProductManager(
        IProductRepository repository,
        ICategoryService categoryService,
        IPricingApi pricingApi,
        IAiRecommendationService aiService,
        IEventPublisher eventPublisher,
        ILogger<ProductManager> logger)
    {
        _repository = repository;
        _categoryService = categoryService;
        _pricingApi = pricingApi;
        _aiService = aiService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }
    
    public async Task<PricingResult> AdjustProductPricingAsync(AdjustPricingDto dto)
    {
        _logger.LogInformation($"Adjusting pricing for {dto.ProductId}");
        
        // Use repository
        var product = await _repository.GetProductAsync(dto.ProductId);
        
        // Use domain service
        var category = await _categoryService.GetCategoryAsync(product.CategoryId);
        
        // Use external API
        var marketPrice = await _pricingApi.GetMarketPrice(product.Sku);
        
        // Use AI service
        var recommendations = await _aiService.GetPricingRecommendations(product.Id);
        
        // Business logic using all services
        var newPrice = CalculateOptimalPrice(dto, marketPrice, recommendations);
        
        // Validate business rules
        if (newPrice < category.MinPrice || newPrice > category.MaxPrice)
            return new PricingResult { IsSuccess = false };
        
        // Update and save
        product.SetPrice(newPrice);
        await _repository.SaveAsync(product);
        
        // Publish event
        await _eventPublisher.PublishAsync(new ProductPriceChanged { ... });
        
        return new PricingResult { IsSuccess = true };
    }
}
```

### Service Dependency Types

| Type | Purpose | Example |
|------|---------|---------|
| **Repository** | Data persistence | IProductRepository |
| **External API** | 3rd party logic | IPricingApi, IPaymentProcessor |
| **Domain Service** | Shared business logic | ICategoryService, IValidationService |
| **AI/ML Service** | Intelligent operations | IAiRecommendationService |
| **Message Bus** | Event publishing | IEventPublisher, IMessageSession |
| **Logger** | Observability | ILogger<T> |

---

## Product Catalog Example

Let's establish three business capabilities for the Product Catalog domain:

### Aggregate Root: Product

```
Product
  ├─ Id (GUID)
  ├─ Sku (string)
  ├─ Name (string)
  ├─ CurrentPrice (decimal)
  ├─ MinPrice (decimal)
  ├─ MaxPrice (decimal)
  ├─ Status (ProductStatus: Draft, Active, Discontinued)
  ├─ FeaturedStatus (Featured, NotFeatured)
  └─ CreatedAt, UpdatedAt (DateTimeOffset)

PriceHistory
  ├─ ProductId (GUID)
  ├─ OldPrice (decimal)
  ├─ NewPrice (decimal)
  ├─ ChangeReason (string)
  └─ EffectiveDate (DateTimeOffset)
```

### Example Capability 1: Promote Product to Featured

**Business Capability**: When marketing decides a product should be featured, the system:
1. Validates the product exists and is active
2. Checks it's not already featured (business rule)
3. Updates product status
4. Publishes ProductPromoted event

**DTO (Structural Validation)**:
```csharp
public class PromoteProductRequest
{
    [Required(ErrorMessage = "Product ID required")]
    public string ProductId { get; set; }
    
    [Required(ErrorMessage = "Featured until date required")]
    public DateTimeOffset FeaturedUntil { get; set; }
}
```

**Manager Function**:
```csharp
public async Task<PromotionResult> PromoteToFeaturedAsync(PromoteProductDto dto)
{
    _logger.LogInformation($"Promoting product {dto.ProductId} to featured status");
    
    try
    {
        // Get product
        var product = await _repository.GetProductAsync(dto.ProductId);
        if (product == null)
            return new PromotionResult { IsSuccess = false, Error = "Product not found" };
        
        // Business Rule 1: Product must be active
        if (product.Status != ProductStatus.Active)
            return new PromotionResult 
            { 
                IsSuccess = false, 
                Error = $"Cannot promote non-active product (status: {product.Status})" 
            };
        
        // Business Rule 2: Cannot promote if already featured
        if (product.FeaturedStatus == FeaturedStatus.Featured)
            return new PromotionResult 
            { 
                IsSuccess = false, 
                Error = "Product is already featured" 
            };
        
        // Business Rule 3: Featured duration must be at least 7 days
        var duration = dto.FeaturedUntil - DateTimeOffset.UtcNow;
        if (duration.TotalDays < 7)
            return new PromotionResult 
            { 
                IsSuccess = false, 
                Error = "Featured period must be at least 7 days" 
            };
        
        // All rules passed - update product
        product.MarkAsFeatured(dto.FeaturedUntil);
        
        // Persist change
        await _repository.SaveAsync(product);
        
        // Publish domain event
        var @event = new ProductPromoted
        {
            ProductId = product.Id,
            ProductName = product.Name,
            PromotedAt = DateTimeOffset.UtcNow,
            FeaturedUntil = dto.FeaturedUntil
        };
        
        await _eventPublisher.PublishAsync(@event);
        
        _logger.LogInformation($"Successfully promoted product {dto.ProductId}");
        
        return new PromotionResult { IsSuccess = true, ProductId = product.Id };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error promoting product {dto.ProductId}");
        return new PromotionResult 
        { 
            IsSuccess = false, 
            Error = "Technical error during promotion", 
            IsRetryable = true 
        };
    }
}

public class PromotionResult
{
    public bool IsSuccess { get; set; }
    public string? ProductId { get; set; }
    public string? Error { get; set; }
    public bool IsRetryable { get; set; }
}
```

### Example Capability 2: Adjust Product Pricing

**Business Capability**: When pricing strategy changes, the system:
1. Validates new price is within bounds
2. Checks price history to prevent excessive discounts
3. Calls external pricing API to validate market rates
4. Updates product and creates price history record
5. Publishes ProductPriceChanged event

**DTO**:
```csharp
public class AdjustPricingRequest
{
    [Required(ErrorMessage = "Product ID required")]
    public string ProductId { get; set; }
    
    [Range(0.01, 999999.99, ErrorMessage = "Price must be between $0.01 and $999,999.99")]
    public decimal NewPrice { get; set; }
    
    [Required(ErrorMessage = "Change reason required")]
    [StringLength(500)]
    public string ChangeReason { get; set; }
}
```

**Manager Function**:
```csharp
public async Task<PricingResult> AdjustProductPricingAsync(
    AdjustPricingDto dto)
{
    _logger.LogInformation($"Adjusting price for product {dto.ProductId} to {dto.NewPrice}");
    
    try
    {
        // Get product
        var product = await _repository.GetProductAsync(dto.ProductId);
        if (product == null)
            return new PricingResult { IsSuccess = false, Error = "Product not found" };
        
        // Business Rule 1: Price within min/max bounds
        if (dto.NewPrice < product.MinPrice || dto.NewPrice > product.MaxPrice)
            return new PricingResult 
            { 
                IsSuccess = false, 
                Error = $"Price must be between {product.MinPrice} and {product.MaxPrice}" 
            };
        
        // Business Rule 2: Validate against price history (no more than 30% discount in 7 days)
        var recentHistory = await _priceHistoryRepository.GetRecentPriceChangesAsync(
            dto.ProductId, 
            dayCount: 7);
        
        var lowestRecentPrice = recentHistory.MinBy(h => h.NewPrice)?.NewPrice ?? product.CurrentPrice;
        var discountPercent = ((lowestRecentPrice - dto.NewPrice) / lowestRecentPrice) * 100;
        
        if (discountPercent > 30m)
            return new PricingResult 
            { 
                IsSuccess = false, 
                Error = "Cannot reduce price more than 30% within 7 days" 
            };
        
        // Business Rule 3: Validate against market pricing (external API)
        var marketData = await _pricingApi.GetMarketPricingAsync(product.Sku);
        if (dto.NewPrice > marketData.MaxCompetitorPrice * 1.2m)
            _logger.LogWarning(
                $"Price {dto.NewPrice} is significantly higher than market ({marketData.MaxCompetitorPrice})");
        
        // All validations passed - update price
        var oldPrice = product.CurrentPrice;
        product.SetPrice(dto.NewPrice);
        await _repository.SaveAsync(product);
        
        // Create price history entry
        var priceHistory = new PriceHistory
        {
            ProductId = product.Id,
            OldPrice = oldPrice,
            NewPrice = dto.NewPrice,
            ChangeReason = dto.ChangeReason,
            EffectiveDate = DateTimeOffset.UtcNow
        };
        await _priceHistoryRepository.SaveAsync(priceHistory);
        
        // Publish domain event
        var @event = new ProductPriceChanged
        {
            ProductId = product.Id,
            ProductName = product.Name,
            OldPrice = oldPrice,
            NewPrice = dto.NewPrice,
            ChangeReason = dto.ChangeReason,
            ChangedAt = DateTimeOffset.UtcNow
        };
        
        await _eventPublisher.PublishAsync(@event);
        
        _logger.LogInformation(
            $"Successfully adjusted price for {dto.ProductId} from {oldPrice} to {dto.NewPrice}");
        
        return new PricingResult 
        { 
            IsSuccess = true, 
            ProductId = product.Id, 
            OldPrice = oldPrice, 
            NewPrice = dto.NewPrice 
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error adjusting pricing for product {dto.ProductId}");
        return new PricingResult 
        { 
            IsSuccess = false, 
            Error = "Technical error during price adjustment", 
            IsRetryable = true 
        };
    }
}

public class PricingResult
{
    public bool IsSuccess { get; set; }
    public string? ProductId { get; set; }
    public decimal? OldPrice { get; set; }
    public decimal? NewPrice { get; set; }
    public string? Error { get; set; }
    public bool IsRetryable { get; set; }
}
```

### Example Capability 3: Generate AI-Powered Pricing Recommendation

**Business Capability**: When marketing needs intelligent pricing guidance, the system:
1. Retrieves product and competitive data
2. Calls AI service for recommendations
3. Validates recommendation against business rules
4. Returns recommendation (without applying it)

**DTO**:
```csharp
public class GetPricingRecommendationRequest
{
    [Required(ErrorMessage = "Product ID required")]
    public string ProductId { get; set; }
    
    [Range(1, 365, ErrorMessage = "Analysis period must be 1-365 days")]
    public int AnalysisDays { get; set; } = 30;
}
```

**Manager Function**:
```csharp
public async Task<RecommendationResult> GetPricingRecommendationAsync(
    GetPricingRecommendationDto dto)
{
    _logger.LogInformation($"Generating pricing recommendation for product {dto.ProductId}");
    
    try
    {
        // Get product
        var product = await _repository.GetProductAsync(dto.ProductId);
        if (product == null)
            return new RecommendationResult { IsSuccess = false, Error = "Product not found" };
        
        // Gather data for AI analysis
        var priceHistory = await _priceHistoryRepository.GetPriceHistoryAsync(
            dto.ProductId, 
            dayCount: dto.AnalysisDays);
        
        var marketData = await _pricingApi.GetMarketPricingAsync(product.Sku);
        var salesData = await _analyticsService.GetSalesDataAsync(dto.ProductId, dto.AnalysisDays);
        
        // Call AI service
        var aiRecommendation = await _aiPricingService.GenerateRecommendationAsync(
            new AiPricingInput
            {
                ProductId = product.Id,
                CurrentPrice = product.CurrentPrice,
                PriceHistory = priceHistory,
                MarketData = marketData,
                SalesData = salesData,
                AnalysisDays = dto.AnalysisDays
            });
        
        // Business Rule: AI recommendation must be within min/max bounds
        if (aiRecommendation.RecommendedPrice < product.MinPrice || 
            aiRecommendation.RecommendedPrice > product.MaxPrice)
        {
            _logger.LogWarning(
                $"AI recommendation {aiRecommendation.RecommendedPrice} outside bounds, clamping");
            
            aiRecommendation.RecommendedPrice = Math.Clamp(
                aiRecommendation.RecommendedPrice, 
                product.MinPrice, 
                product.MaxPrice);
        }
        
        // Return recommendation without applying it
        return new RecommendationResult
        {
            IsSuccess = true,
            ProductId = product.Id,
            CurrentPrice = product.CurrentPrice,
            RecommendedPrice = aiRecommendation.RecommendedPrice,
            Confidence = aiRecommendation.Confidence,
            Rationale = aiRecommendation.Rationale,
            ProjectedSalesIncrease = aiRecommendation.ProjectedSalesIncrease
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error generating recommendation for product {dto.ProductId}");
        return new RecommendationResult 
        { 
            IsSuccess = false, 
            Error = "Technical error generating recommendation", 
            IsRetryable = true 
        };
    }
}

public class RecommendationResult
{
    public bool IsSuccess { get; set; }
    public string? ProductId { get; set; }
    public decimal? CurrentPrice { get; set; }
    public decimal? RecommendedPrice { get; set; }
    public decimal? Confidence { get; set; }
    public string? Rationale { get; set; }
    public decimal? ProjectedSalesIncrease { get; set; }
    public string? Error { get; set; }
    public bool IsRetryable { get; set; }
}
```

---

## Dependency Injection Wiring

### Azure Functions Host Configuration

The `Program.cs` file must register all Manager dependencies for both API and Event Handler projects.

### API Project (Product.Api)

```csharp
// Product/src/Api/Program.cs
using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Cosmos;
using Product.Domain.Managers;
using Product.Domain.Managers.Interfaces;
using Product.Infrastructure.Repositories;
using Product.Infrastructure.Services;
using Product.Infrastructure.ExternalApis;

[assembly: Microsoft.Azure.Functions.Worker.FunctionsMetadata.AzureFunctionsMetadata]

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // ============================================
        // Core Dependencies
        // ============================================
        
        // CosmosDB Client (singleton - expensive to create)
        services.AddSingleton(provider => new CosmosClient(
            Environment.GetEnvironmentVariable("CosmosDbConnectionString") 
                ?? "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDL5z+YeIXoaofM15/8j7aG/E20zOw=="
        ));
        
        // ============================================
        // Repositories (Scoped - per request)
        // ============================================
        services.AddScoped(provider => 
        {
            var cosmosClient = provider.GetRequiredService<CosmosClient>();
            return new ProductRepository(
                cosmosClient,
                databaseId: "ProductCatalogDB",
                containerId: "Products"
            );
        });
        
        services.AddScoped(provider => 
        {
            var cosmosClient = provider.GetRequiredService<CosmosClient>();
            return new PriceHistoryRepository(
                cosmosClient,
                databaseId: "ProductCatalogDB",
                containerId: "PriceHistory"
            );
        });
        
        // ============================================
        // Domain Services (Scoped - per request)
        // ============================================
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IValidationService, ValidationService>();
        
        // ============================================
        // External APIs & HTTP Clients (Scoped)
        // ============================================
        services.AddHttpClient<IPricingApi, PricingApi>(client =>
        {
            client.BaseAddress = new Uri(
                Environment.GetEnvironmentVariable("PricingApiBaseUrl") 
                    ?? "https://api.competitor-pricing.com");
            client.DefaultRequestHeaders.Add("X-API-Key", 
                Environment.GetEnvironmentVariable("PricingApiKey"));
        });
        
        services.AddHttpClient<IAiRecommendationService, AiRecommendationService>(client =>
        {
            client.BaseAddress = new Uri(
                Environment.GetEnvironmentVariable("AiServiceBaseUrl") 
                    ?? "https://api.aipricing-service.com");
        });
        
        // ============================================
        // Analytics Service (Scoped)
        // ============================================
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        
        // ============================================
        // Event Publishing (Scoped)
        // ============================================
        services.AddScoped<IEventPublisher, NServiceBusEventPublisher>();
        
        // ============================================
        // MANAGERS (Scoped - per request)
        // ============================================
        // ProductManager depends on all above services
        services.AddScoped<IProductManager, ProductManager>();
        
        // ============================================
        // Logging (built-in)
        // ============================================
        // ILogger<T> is automatically registered
    })
    .Build();

host.Run();
```

### Event Handler Endpoint Configuration

```csharp
// Product/src/Endpoint.In/Program.cs
using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Cosmos;
using Product.Domain.Managers;
using Product.Domain.Managers.Interfaces;
using Product.Infrastructure.Repositories;
using Product.Infrastructure.Services;
using Product.Infrastructure.ExternalApis;
using NServiceBus;

[assembly: NServiceBusTriggerFunction("ProductMessageWorker")]

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // ============================================
        // IDENTICAL to API project
        // ============================================
        
        // CosmosDB Client
        services.AddSingleton(provider => new CosmosClient(
            Environment.GetEnvironmentVariable("CosmosDbConnectionString")
                ?? "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDL5z+YeIXoaofM15/8j7aG/E20zOw=="
        ));
        
        // Repositories
        services.AddScoped(provider => 
        {
            var cosmosClient = provider.GetRequiredService<CosmosClient>();
            return new ProductRepository(
                cosmosClient,
                databaseId: "ProductCatalogDB",
                containerId: "Products"
            );
        });
        
        services.AddScoped(provider => 
        {
            var cosmosClient = provider.GetRequiredService<CosmosClient>();
            return new PriceHistoryRepository(
                cosmosClient,
                databaseId: "ProductCatalogDB",
                containerId: "PriceHistory"
            );
        });
        
        // Domain Services
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IValidationService, ValidationService>();
        
        // External APIs
        services.AddHttpClient<IPricingApi, PricingApi>(client =>
        {
            client.BaseAddress = new Uri(
                Environment.GetEnvironmentVariable("PricingApiBaseUrl")
                    ?? "https://api.competitor-pricing.com");
            client.DefaultRequestHeaders.Add("X-API-Key",
                Environment.GetEnvironmentVariable("PricingApiKey"));
        });
        
        services.AddHttpClient<IAiRecommendationService, AiRecommendationService>(client =>
        {
            client.BaseAddress = new Uri(
                Environment.GetEnvironmentVariable("AiServiceBaseUrl")
                    ?? "https://api.aipricing-service.com");
        });
        
        // Analytics
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        
        // Event Publishing
        services.AddScoped<IEventPublisher, NServiceBusEventPublisher>();
        
        // Manager
        services.AddScoped<IProductManager, ProductManager>();
    })
    .NServiceBusEnvironmentConfiguration("ProductMessageWorker")
    .Build();

// Ensure CosmosDB is initialized
var cosmosClient = host.Services.GetRequiredService<CosmosClient>();
CosmosDbInitializer.EnsureDbAndContainerAsync(
    cosmosClient,
    "ProductCatalogDB",
    "Products",
    "/CategoryId"
).GetAwaiter().GetResult();

host.Run();
```

### Key Wiring Principles

| Principle | Implementation | Why |
|-----------|-----------------|-----|
| **Same Manager** | Both API and Handler use `IProductManager` | Single source of truth for business logic |
| **Same Repositories** | Both projects inject `IProductRepository` | Data access is consistent |
| **Same Services** | Both register `ICategoryService`, etc. | Domain logic shared everywhere |
| **Singleton CosmosClient** | Registered once, shared across requests | Expensive to create, thread-safe |
| **Scoped Managers** | New instance per request | Prevents cross-request state pollution |
| **Scoped Repositories** | New instance per request | Clean data boundaries |
| **Named HTTP Clients** | `AddHttpClient<IService, Implementation>` | Type-safe, reusable clients |

---

## Sequence Diagrams

### Scenario 1: API Call to Promote Product

```
┌─────────┐         ┌────────┐      ┌──────────┐      ┌──────────────┐      ┌──────┐
│  Client │         │  API   │      │ Manager  │      │  Repository  │      │  DB  │
└────┬────┘         └───┬────┘      └─────┬────┘      └──────┬───────┘      └──┬───┘
     │                  │                 │                  │                  │
     │  POST /promote   │                 │                  │                  │
     ├─────────────────→│                 │                  │                  │
     │                  │                 │                  │                  │
     │                  │ Deserialize     │                  │                  │
     │                  │ & Validate DTO  │                  │                  │
     │                  │ [PromoteRequest]│                  │                  │
     │                  │                 │                  │                  │
     │                  │ Map to Domain   │                  │                  │
     │                  │ DTO             │                  │                  │
     │                  │                 │                  │                  │
     │                  │ PromoteToFeatured(dto)             │                  │
     │                  ├────────────────→│                  │                  │
     │                  │                 │                  │                  │
     │                  │                 │ GetProductAsync  │                  │
     │                  │                 ├─────────────────→│                  │
     │                  │                 │                  │  Query {id}      │
     │                  │                 │                  ├─────────────────→│
     │                  │                 │                  │                  │
     │                  │                 │                  │        Product   │
     │                  │                 │                  │←─────────────────┤
     │                  │                 │                  │                  │
     │                  │                 │ Product          │                  │
     │                  │                 │←─────────────────┤                  │
     │                  │                 │                  │                  │
     │                  │                 │ Validate         │                  │
     │                  │                 │ Business Rules   │                  │
     │                  │                 │ ✓ Active?        │                  │
     │                  │                 │ ✓ Not featured?  │                  │
     │                  │                 │ ✓ Min 7 days?    │                  │
     │                  │                 │                  │                  │
     │                  │                 │ SaveAsync        │                  │
     │                  │                 ├─────────────────→│                  │
     │                  │                 │                  │  Upsert Product  │
     │                  │                 │                  ├─────────────────→│
     │                  │                 │                  │                  │
     │                  │                 │                  │        OK        │
     │                  │                 │                  │←─────────────────┤
     │                  │                 │                  │                  │
     │                  │                 │ OK               │                  │
     │                  │                 │←─────────────────┤                  │
     │                  │                 │                  │                  │
     │                  │                 │ PublishAsync     │                  │
     │                  │                 │ (ProductPromoted)│                  │
     │                  │                 │ [NServiceBus]    │                  │
     │                  │                 │                  │                  │
     │                  │ PromotionResult │                  │                  │
     │                  │←────────────────┤                  │                  │
     │                  │                 │                  │                  │
     │  201 Created     │                 │                  │                  │
     │←─────────────────┤                 │                  │                  │
     │ { id, status }   │                 │                  │                  │
```

### Scenario 2: Event Handler Processing Same Manager Call

```
┌──────────┐         ┌─────────────┐      ┌──────────┐      ┌──────────────┐      ┌──────┐
│ Service  │         │   Handler   │      │ Manager  │      │  Repository  │      │  DB  │
│  Bus     │         │             │      │          │      │              │      │      │
└────┬─────┘         └──────┬──────┘      └─────┬────┘      └──────┬───────┘      └──┬───┘
     │                      │                   │                  │                  │
     │ PromoteProductCmd    │                   │                  │                  │
     ├─────────────────────→│                   │                  │                  │
     │                      │                   │                  │                  │
     │                      │ NServiceBus       │                  │                  │
     │                      │ Deserializes Cmd  │                  │                  │
     │                      │ (automatic)       │                  │                  │
     │                      │                   │                  │                  │
     │                      │ Map to Domain DTO │                  │                  │
     │                      │                   │                  │                  │
     │                      │ PromoteToFeatured(dto)               │                  │
     │                      ├──────────────────→│                  │                  │
     │                      │                   │                  │                  │
     │                      │                   │ GetProductAsync  │                  │
     │                      │                   ├─────────────────→│                  │
     │                      │                   │                  │  Query {id}      │
     │                      │                   │                  ├─────────────────→│
     │                      │                   │                  │                  │
     │                      │                   │                  │        Product   │
     │                      │                   │                  │←─────────────────┤
     │                      │                   │                  │                  │
     │                      │                   │ Product          │                  │
     │                      │                   │←─────────────────┤                  │
     │                      │                   │                  │                  │
     │                      │                   │ [Business Logic] │                  │
     │                      │                   │                  │                  │
     │                      │                   │ SaveAsync        │                  │
     │                      │                   ├─────────────────→│                  │
     │                      │                   │                  │  Upsert Product  │
     │                      │                   │                  ├─────────────────→│
     │                      │                   │                  │                  │
     │                      │                   │                  │        OK        │
     │                      │                   │                  │←─────────────────┤
     │                      │                   │                  │                  │
     │                      │                   │ OK               │                  │
     │                      │                   │←─────────────────┤                  │
     │                      │                   │                  │                  │
     │                      │ PromotionResult   │                  │                  │
     │                      │←──────────────────┤                  │                  │
     │                      │                   │                  │                  │
     │                      │ Publish           │                  │                  │
     │                      │ ProductPromoted   │                  │                  │
     │                      │ Event             │                  │                  │
     │                      │ [NServiceBus]     │                  │                  │
     │                      │                   │                  │                  │
     │      Event           │                   │                  │                  │
     │←─────────────────────┤                   │                  │                  │
     │                      │                   │                  │                  │
```

### Key Observation

Both diagrams show the **same** Manager function being called with the **same** DTO, proving that the Manager is protocol-agnostic. The only difference is where the request originates (API vs endpoint message) and how the response is handled (HTTP vs Event Publishing).

---

## Common Patterns

### Pattern 1: Result Objects for Complex Operations

Use result objects to communicate success/failure without throwing exceptions:

```csharp
public class OperationResult
{
    public bool IsSuccess { get; set; }
    public string? Error { get; set; }
    public bool IsRetryable { get; set; }
}

public class OperationResult<T> : OperationResult
{
    public T? Data { get; set; }
}

// Usage in Manager
public async Task<OperationResult<string>> CreateProductAsync(CreateProductDto dto)
{
    try
    {
        // Business logic
        var product = new Product { ... };
        await _repository.SaveAsync(product);
        return new OperationResult<string> 
        { 
            IsSuccess = true, 
            Data = product.Id 
        };
    }
    catch (Exception ex)
    {
        return new OperationResult<string>
        {
            IsSuccess = false,
            Error = ex.Message,
            IsRetryable = IsRetryable(ex)
        };
    }
}
```

### Pattern 2: Handler Result Mapping

Handlers map Manager results to domain events:

```csharp
public async Task Handle(CreateProductCommand command, IMessageHandlerContext context)
{
    var dto = MapCommandToDto(command);
    var result = await _manager.CreateProductAsync(dto);
    
    if (result.IsSuccess)
    {
        await context.Publish(new ProductCreated 
        { 
            ProductId = result.Data,
            CreatedAt = DateTimeOffset.UtcNow 
        });
    }
    else
    {
        await context.Publish(new ProductCreationFailed 
        { 
            Error = result.Error,
            IsRetryable = result.IsRetryable 
        });
    }
}
```

### Pattern 3: Exception Handling Strategy

- **Manager**: Catches exceptions, returns result objects (non-throwing)
- **API**: Catches exceptions, returns HTTP error responses
- **Handler**: Catches exceptions, publishes failure events

```csharp
// Manager - non-throwing
public async Task<PromotionResult> PromoteToFeaturedAsync(PromoteProductDto dto)
{
    try
    {
        // ... business logic
        return new PromotionResult { IsSuccess = true };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in PromoteToFeatured");
        return new PromotionResult 
        { 
            IsSuccess = false, 
            Error = "Technical error", 
            IsRetryable = IsRetryable(ex) 
        };
    }
}

// API - converts Manager result to HTTP response
public async Task<HttpResponseData> PromoteProduct(HttpRequestData req)
{
    try
    {
        var result = await _manager.PromoteToFeaturedAsync(dto);
        
        if (!result.IsSuccess)
            return req.CreateResponse(BadRequest);
        
        return req.CreateResponse(OK);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in API handler");
        return req.CreateResponse(InternalServerError);
    }
}
```

### Pattern 4: Logging Levels

- **Debug**: Detailed operation steps (entering manager, calling services)
- **Information**: Key operations (product created, promotion successful)
- **Warning**: Business rule violations (price exceeds limit, duplicate detected)
- **Error**: Technical exceptions (database error, external API failure)

```csharp
public async Task<PromotionResult> PromoteToFeaturedAsync(PromoteProductDto dto)
{
    _logger.LogInformation($"Starting promotion of product {dto.ProductId}");
    
    var product = await _repository.GetProductAsync(dto.ProductId);
    _logger.LogDebug($"Retrieved product: {product.Name}");
    
    if (product.IsAlreadyFeatured)
    {
        _logger.LogWarning($"Product {dto.ProductId} already featured");
        return new PromotionResult { ... };
    }
    
    // ... proceed with promotion
    
    _logger.LogInformation($"Successfully promoted product {dto.ProductId}");
    return new PromotionResult { IsSuccess = true };
}
```

---

## Scaffolding Checklist

When creating a new domain with this pattern, follow this checklist:

### 1. Define Aggregate Root
- [ ] Entity model with business methods
- [ ] Value objects for complex properties
- [ ] Invariants and business rules
- [ ] Domain events collection (if needed)

### 2. Create DTOs
- [ ] Request DTO with data annotations
- [ ] Domain DTO (may differ from request)
- [ ] Result DTO for response
- [ ] Validation attributes on Request DTO

### 3. Create Manager Interface & Implementation
- [ ] Business capability methods (not CRUD)
- [ ] Constructor injection of dependencies
- [ ] Business rule validation
- [ ] Repository calls
- [ ] External service calls
- [ ] Event publishing
- [ ] Error handling with result objects
- [ ] Structured logging

### 4. Create API Function
- [ ] HTTP trigger function
- [ ] DTO deserialization
- [ ] DTO validation
- [ ] Manager invocation
- [ ] Result mapping to HTTP response
- [ ] Error handling

### 5. Create Event Handler
- [ ] IHandleMessages<TCommand> implementation
- [ ] Command deserialization (automatic)
- [ ] DTO validation
- [ ] Manager invocation
- [ ] Event publishing based on result
- [ ] Error handling

### 6. Wire Dependencies in Program.cs
- [ ] CosmosDB client (singleton)
- [ ] Repository registration (scoped)
- [ ] Domain service registration (scoped)
- [ ] External API registration (scoped HTTP client)
- [ ] Manager registration (scoped)
- [ ] Event publisher registration (scoped)

### 7. Documentation
- [ ] Document business capabilities
- [ ] Document business rules
- [ ] Document validation rules
- [ ] Document service dependencies
- [ ] Document error scenarios

---

## Summary

This Manager Architecture Pattern establishes clear boundaries and responsibilities:

| Layer | Responsibility | Protocol-Agnostic? |
|-------|-----------------|-------------------|
| **API/Handler** | Protocol translation, DTO validation | ❌ Protocol-specific |
| **Manager** | Business logic, service orchestration | ✅ Pure C# |
| **Services** | Data access, external integrations | ✅ Pure C# |

The same Manager function is called by multiple entry points (API, Handler), proving it's truly protocol-agnostic. This enables:

- ✅ Single source of truth for business logic
- ✅ Easy testing (mock dependencies)
- ✅ Consistent behavior across protocols
- ✅ Clear separation of concerns
- ✅ Scalable architecture

Follow this constitution when scaffolding new domains to ensure consistency and maintainability across the platform.
