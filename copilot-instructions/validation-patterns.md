# ⚠️ VALIDATION PATTERNS - TO BE DOCUMENTED

## Status: PLACEHOLDER

**This file is a placeholder for validation patterns and standards that need to be documented.**

When documenting this section, include:

## Topics to Cover

### Validation Layers
- API validation (format/structure)
- Domain validation (business rules)
- When to validate at each layer
- Avoiding duplicate validation

### Input Validation (API Layer)
- Data Annotations validation
- ModelState validation
- Custom validation attributes
- Manual validation checks

```csharp
// Examples needed
public class CreateEventRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; }
    
    [Required]
    public DateTime StartDate { get; set; }
    
    [Required]
    [CustomValidation(...)]
    public DateTime EndDate { get; set; }
}
```

### Domain Validation (Domain Layer)
- Validation in entity constructors
- Validation in entity methods
- Business rule validation
- Throwing domain exceptions

```csharp
// Example needed
public class Event
{
    public static Event Create(string name, DateTime startDate, DateTime endDate)
    {
        // Validation logic to be documented
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Name is required");
        
        if (endDate <= startDate)
            throw new ValidationException("End date must be after start date");
        
        // Create entity
    }
}
```

### FluentValidation (If Used)
- Setting up FluentValidation
- Creating validators
- Integrating with ASP.NET Core
- Async validation rules

```csharp
// Example if FluentValidation is used
public class CreateEventRequestValidator : AbstractValidator<CreateEventRequest>
{
    public CreateEventRequestValidator()
    {
        // To be documented
    }
}
```

### Validation Error Responses
- Standard error format
- Multiple validation errors
- Localization considerations
- User-friendly messages

```json
// Example error response format
{
  "errors": {
    "Name": ["The Name field is required."],
    "EndDate": ["End date must be after start date."]
  }
}
```

### Command Validation
- Validating commands before sending
- Validation in command handlers
- Rejecting invalid commands early

### Invariant Enforcement
- Entity invariants
- Aggregate boundary protection
- Preventing invalid state

### Async Validation
- External service validation
- Database uniqueness checks
- Performance considerations

### Cross-Field Validation
- Validating relationships between fields
- Complex business rules
- Multi-step validation

### Validation Testing
- Testing validation rules
- Parameterized tests for edge cases
- Invalid input tests

```csharp
// Test examples needed
[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
public void Create_InvalidName_ThrowsValidationException(string invalidName)
{
    // To be documented
}
```

### Custom Validation Attributes
- Creating reusable validators
- Complex validation logic
- Composing validators

### Validation vs Business Rules
- Distinguishing between validation and logic
- Where each belongs
- Examples of each

### Error Messages
- Clear and actionable messages
- Avoiding technical jargon
- Consistency in messaging
- Localization support

## Example Patterns Needed

### API Controller Validation
```csharp
[HttpPost]
public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }
    
    // Additional validation to be documented
}
```

### Domain Entity Validation
```csharp
public class Event
{
    private Event() { } // Force use of factory method
    
    public static Event Create(string name, DateTime startDate, DateTime endDate)
    {
        ValidateName(name);
        ValidateDateRange(startDate, endDate);
        
        // Create and return entity
    }
    
    private static void ValidateName(string name)
    {
        // Validation logic to be documented
    }
    
    private static void ValidateDateRange(DateTime start, DateTime end)
    {
        // Validation logic to be documented
    }
}
```

### Command Handler Validation
```csharp
public class CreateEventCommandHandler : IHandleMessages<CreateEventCommand>
{
    public async Task Handle(CreateEventCommand message, IMessageHandlerContext context)
    {
        // Validation in handler to be documented
        
        // If validation fails, publish failure event
        await context.Publish(new EventCreationFailedEvent
        {
            EventId = message.EventId,
            Reason = "Validation failed: ..."
        });
    }
}
```

## Action Required

**This section needs to be filled out with:**
1. Review existing validation patterns in the codebase
2. Document validation layer responsibilities
3. Define validation standards
4. Provide code examples for common scenarios
5. Document error response formats
6. Create testing guidelines for validation
7. Document FluentValidation usage (if applicable)

**Priority**: High - Validation is critical for data integrity

**Owner**: To be assigned

**Related Files**:
- See [api-conventions.md](api-conventions.md) for API validation
- See [domain-events.md](domain-events.md) for validation failure events
- See [error-handling.md](error-handling.md) for validation error handling
- See [testing-standards.md](testing-standards.md) for validation testing
