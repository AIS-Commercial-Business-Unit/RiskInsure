# API Conventions

## Overview

This document defines the conventions and patterns for building RESTful HTTP APIs using ASP.NET Core. The API layer serves as the HTTP interface to the bounded context, handling requests and publishing commands to the message bus.

## Controller Organization

### One Manager Per Controller (Preferred Pattern)

**Guideline**: Controllers SHOULD be organized around a single manager to maintain clear separation of concerns and improve maintainability.

**Rationale**:
- **Clear Responsibility**: Each controller has one focused business capability
- **Easier Testing**: Simpler to mock and test with fewer dependencies
- **Better Maintainability**: Changes to one capability don't affect others
- **Logical Grouping**: Routes naturally align with business capabilities

**Example Structure**:
```
Controllers/
├── BillingAccountsController.cs    → IBillingAccountManager (account lifecycle)
├── BillingPaymentsController.cs    → IBillingPaymentManager (payment recording)
└── ProductsController.cs           → IProductManager (product operations)
```

**When Multiple Managers Are Acceptable**:
- When managers represent closely related sub-capabilities of the same resource
- When the controller serves as a thin facade with minimal logic
- When explicitly confirmed as the intended design pattern
- When refactoring would create unnecessary complexity

**If you need multiple managers in one controller**, confirm this is intentional and document the rationale in code comments.

**Example of Multiple Managers (with confirmation)**:
```csharp
/// <summary>
/// Combined billing operations controller.
/// NOTE: Uses multiple managers - BillingAccountManager for account lifecycle 
/// and BillingPaymentManager for payment recording. This is intentional to 
/// provide a unified /api/billing/* endpoint structure.
/// </summary>
[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly IBillingAccountManager _accountManager;
    private readonly IBillingPaymentManager _paymentManager;
    
    // Endpoints clearly separated by concern...
}
```

## Example Scenario

Throughout this document, we use a **Product Catalog** domain as our example:
- **Resource**: Products (items in a catalog)
- **Operations**: Create, update, retrieve, delete products
- **Domain Entities**: Product, Category, Inventory
- **Sample Commands**: CreateProduct, UpdatePrice, AdjustInventory
- **Sample Events**: ProductCreated, PriceChanged, InventoryAdjusted

This is a generic scenario applicable across many domains. Replace "Product" with your domain entity (Order, Customer, Invoice, etc.).

## RESTful Design Principles

### Resource-Oriented URLs
Use nouns (resources), not verbs:
```
✅ GET    /api/products            # List all products
✅ GET    /api/products/{id}       # Get single product
✅ POST   /api/products            # Create new product
✅ PUT    /api/products/{id}       # Update entire product
✅ PATCH  /api/products/{id}       # Partial update
✅ DELETE /api/products/{id}       # Delete product

❌ GET    /api/getProduct?id=123
❌ POST   /api/createProduct
```

### HTTP Methods
- **GET**: Retrieve resource(s), idempotent, no side effects
- **POST**: Create new resource or trigger action
- **PUT**: Update entire resource, idempotent
- **PATCH**: Partial update
- **DELETE**: Remove resource, idempotent

### Standard HTTP Status Codes

**Success Codes**:
- `200 OK`: Successful GET, PUT, PATCH, or synchronous operation
- `201 Created`: Resource created, include `Location` header
- `202 Accepted`: **Primary pattern - async command accepted**
- `204 No Content`: Successful DELETE or operation with no return value

**Client Error Codes**:
- `400 Bad Request`: Invalid request format, validation failure
- `401 Unauthorized`: Authentication required
- `403 Forbidden`: Authenticated but not authorized
- `404 Not Found`: Resource doesn't exist
- `409 Conflict`: Concurrent modification, business rule violation
- `422 Unprocessable Entity`: Semantic validation failure

**Server Error Codes**:
- `500 Internal Server Error`: Unexpected server error
- `503 Service Unavailable`: Temporary unavailability

## Async Processing Pattern

### Standard Pattern
Most operations use **fire-and-forget** with 202 Accepted:

```csharp
[HttpPost]
[Route("api/products")]
public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
{
    // 1. Validate request format
    if (!ModelState.IsValid)
        return BadRequest(ModelState);
    
    // 2. Send command to message bus
    await _messageSession.Send(new CreateProduct
    {
        MessageId = Guid.NewGuid(),
        OccurredUtc = DateTimeOffset.UtcNow,
        ProductId = Guid.NewGuid(),
        Name = request.Name,
        Price = request.Price,
        IdempotencyKey = $"create-product-{request.Name}"
    });
    
    // 3. Return 202 Accepted
    return Accepted(new 
    { 
        message = "Product creation initiated",
        productId = productId
    });
}
```

### Why 202 Accepted?
- Acknowledges request receipt
- Indicates async processing
- Doesn't wait for completion
- Scales better under load
- Prevents timeout issues

### When to Use 200 OK
Use synchronous responses only when:
- Simple queries with no side effects
- Data immediately available
- No complex business logic
- Low latency operations

```csharp
[HttpGet]
[Route("api/products/{id}")]
public async Task<IActionResult> GetProduct(Guid id)
{
    var product = await _repository.GetByIdAsync(id);
    
    if (product == null)
        return NotFound();
    
    return Ok(new ProductResponse
    {
        Id = product.Id,
        Name = product.Name,
        Price = product.Price,
        Status = product.Status.ToString()
    });
}
```

## Request/Response Models

### Request DTOs
```csharp
// API/Models/CreateProductRequest.cs
public class CreateProductRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; }
    
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }
    
    [StringLength(50)]
    public string? Category { get; set; }
}
```

### Response DTOs
```csharp
// API/Models/ProductResponse.cs
public class ProductResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; }
    public string? Category { get; set; }
}
```

### Best Practices
- Separate request/response models from domain entities
- Use Data Annotations for basic validation
- Keep DTOs simple (no logic)
- Use appropriate data types
- Document with XML comments
- **Client-Generated IDs**: Clients should generate resource IDs (GUIDs) to enable idempotency, cross-domain coordination, and client-side correlation

## Validation

### Two-Layer Validation

**Layer 1: API (Format Validation)**
```csharp
[HttpPost]
public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
{
    // Automatic model validation
    if (!ModelState.IsValid)
        return BadRequest(ModelState);
    
    // Additional format checks
    if (request.Price <= 0)
        return BadRequest("Price must be greater than zero");
    
    // Send to message bus
    await _messageSession.Send(new CreateProduct { /* ... */ });
    return Accepted();
}
```

**Layer 2: Domain (Business Validation)**
- Performed in message handlers
- Validates business rules
- Ensures invariants
- Failures published as events

### Validation Response Format
```json
{
  "errors": {
    "Name": ["The Name field is required."],
    "Price": ["Price must be greater than zero"]
  }
}
```

## Error Handling

### Exception Filter
```csharp
public class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;
    
    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, "API error occurred");
        
        var result = context.Exception switch
        {
            ValidationException ex => new BadRequestObjectResult(new { error = ex.Message }),
            NotFoundException ex => new NotFoundObjectResult(new { error = ex.Message }),
            _ => new ObjectResult(new { error = "An error occurred" }) 
                { StatusCode = 500 }
        };
        
        context.Result = result;
        context.ExceptionHandled = true;
    }
}
```

### Error Response Format
```json
{
  "error": "Detailed error message",
  "traceId": "correlation-id-123"
}
```

## API Versioning

### URL-Based Versioning
```
/api/v1/products
/api/v2/products
```

### Implementation
```csharp
[ApiController]
[Route("api/v1/products")]
public class ProductsV1Controller : ControllerBase
{
    // V1 endpoints
}

[ApiController]
[Route("api/v2/products")]
public class ProductsV2Controller : ControllerBase
{
    // V2 endpoints with breaking changes
}
```

### Version Strategy
- Start with v1
- Maintain backwards compatibility when possible
- Create new version for breaking changes
- Deprecate old versions gradually

## OpenAPI Documentation

### XML Documentation
```csharp
/// <summary>
/// Creates a new product
/// </summary>
/// <param name="request">Product details</param>
/// <returns>Accepted response with product ID</returns>
/// <response code="202">Product creation initiated</response>
/// <response code="400">Invalid request</response>
[HttpPost]
[ProducesResponseType(StatusCodes.Status202Accepted)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
{
    // Implementation
}
```

**Note**: For implementation details on configuring OpenAPI/Scalar, see `init.api.md`.

## Health Checks

### Endpoints
- `/health`: Overall health
- `/health/ready`: Readiness probe (dependencies available)
- `/health/live`: Liveness probe (process running)

**Note**: For implementation details on configuring health checks, see `init.api.md`.

## CORS Configuration

**Development**: Allow all origins, methods, headers
**Production**: Restrict to specific origins with credentials

**Note**: For implementation details, see `init.api.md`.

## Authentication & Authorization

### Placeholder for Future Implementation
```csharp
// To be implemented based on requirements
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // JWT configuration
    });

builder.Services.AddAuthorization(options =>
{
    // Authorization policies
});

app.UseAuthentication();
app.UseAuthorization();
```

### Securing Endpoints
```csharp
[Authorize]
[HttpPost]
[Route("api/events")]
public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
{
    // Only authenticated users
}

[Authorize(Policy = "AdminOnly")]
[HttpDelete]
[Route("api/events/{id}")]
public async Task<IActionResult> DeleteEvent(Guid id)
{
    // Only administrators
}
```

## Logging

### Request Logging
```csharp
[HttpPost]
public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
{
    _logger.LogInformation(
        "Creating product: {ProductName} with price {Price}",
        request.Name, 
        request.Price);
    
    try
    {
        await _messageSession.Send(command);
        _logger.LogInformation("Product creation command sent: {ProductId}", productId);
        return Accepted();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to send product creation command");
        throw;
    }
}
```

### Log Levels
- **Debug**: Detailed diagnostic information
- **Information**: General flow (requests, commands sent)
- **Warning**: Unexpected but handled situations
- **Error**: Failures and exceptions
- **Critical**: System failures requiring immediate attention

## Content Negotiation

### JSON Default
Use camelCase for JSON properties. Ignore null values by default.

**Note**: For configuration details, see `init.api.md`.

## Rate Limiting

### Placeholder for Future Implementation
```csharp
// Consider using AspNetCoreRateLimit package
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Limit = 100,
            Period = "1m"
        }
    };
});
```

## Best Practices

### Do's ✅
1. Use 202 Accepted for async operations
2. Validate at API boundary (format/structure)
3. Publish commands, don't call handlers directly
4. Return meaningful error messages
5. Document endpoints with Swagger
6. Implement health checks
7. Log requests and errors
8. Use DTOs (don't expose domain entities)
9. Handle exceptions gracefully
10. Version breaking API changes
11. **Prefer one manager per controller for clarity and separation of concerns**
12. **Use client-generated IDs (GUIDs) for resource creation** to enable idempotency, distributed coordination, and request correlation

### Don'ts ❌
1. Don't include business logic in controllers
2. Don't query database directly from API
3. Don't wait for message processing to complete
4. Don't expose internal implementation details
5. Don't return stack traces in production
6. Don't ignore validation
7. Don't use synchronous processing for complex operations
8. Don't couple API to domain entities
9. Don't forget CORS in production
10. Don't log sensitive data (PII, credentials)

## Example Complete Endpoint

```csharp
/// <summary>
/// Creates a new product
/// </summary>
[HttpPost]
[Route("api/products")]
[ProducesResponseType(typeof(CreateProductResponse), StatusCodes.Status202Accepted)]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> CreateProduct(
    [FromBody] CreateProductRequest request,
    CancellationToken cancellationToken)
{
    _logger.LogInformation("Received product creation request: {ProductName}", request.Name);
    
    // Format validation
    if (!ModelState.IsValid)
    {
        _logger.LogWarning("Invalid product creation request");
        return BadRequest(ModelState);
    }
    
    if (request.Price <= 0)
    {
        _logger.LogWarning("Invalid price: Price must be greater than zero");
        return BadRequest(new { error = "Price must be greater than zero" });
    }
    
    // Generate ID and idempotency key
    var productId = Guid.NewGuid();
    var idempotencyKey = $"create-product-{request.Name}-{DateTimeOffset.UtcNow:yyyyMMdd}";
    
    // Send command
    var command = new CreateProduct
    {
        MessageId = Guid.NewGuid(),
        OccurredUtc = DateTimeOffset.UtcNow,
        ProductId = productId,
        Name = request.Name,
        Price = request.Price,
        Category = request.Category,
        IdempotencyKey = idempotencyKey
    };
    
    await _messageSession.Send(command, cancellationToken);
    
    _logger.LogInformation("Product creation command sent: {ProductId}", productId);
    
    // Return 202 Accepted
    return Accepted(new CreateProductResponse
    {
        ProductId = productId,
        Message = "Product creation initiated"
    });
}
```

## Testing APIs

### Integration Tests
```csharp
public class ProductsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    
    [Fact]
    public async Task CreateProduct_ValidRequest_Returns202()
    {
        // Arrange
        var request = new CreateProductRequest
        {
            Name = "Test Product",
            Price = 99.99m,
            Category = "Electronics"
        };
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/products", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
```
