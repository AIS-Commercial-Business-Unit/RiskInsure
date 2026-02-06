# E2E Contract Verifier Prompt

**Purpose**: Verify E2E test helper contracts match API controller request/response models  
**Target**: All domains with E2E test coverage  
**Execution Time**: 5-10 minutes per domain  
**Generates**: Contract mismatch report and fix recommendations

---

## Instructions for AI Agent

You are verifying that E2E test contracts align with API models across all RiskInsure domains. This prevents 400 Bad Request errors caused by contract mismatches.

### Step 1: Identify E2E Test Helpers

**Find all test helper files:**
```powershell
Get-ChildItem test/e2e/helpers/*-api.ts | Select-Object Name
```

For each helper file found:
1. Extract TypeScript interface definitions
2. Note field names, types, required vs optional (`?`)
3. Document any default values in helper functions

**Example extraction**:
```typescript
// From customer-api.ts
export interface CreateCustomerRequest {
  firstName: string;           // Required
  lastName: string;            // Required
  email: string;               // Required
  phone: string;               // Required
  address: {                   // Required nested object
    street: string;
    city: string;
    state: string;
    zipCode: string;
  };
  birthDate?: DateTimeOffset;  // Optional
}
```

### Step 2: Locate Corresponding API Models

For each domain identified from test helpers (e.g., `customer-api.ts` → Customer domain):

**Find API request models:**
```powershell
# Pattern: services/{domain}/src/Api/Models/*Request.cs
Get-ChildItem services/customer/src/Api/Models/*Request.cs
```

**Read and analyze**:
1. Check `[Required]` attributes
2. Note property names (PascalCase in C#)
3. Identify nullable types (`?` or `Nullable<T>`)
4. Check for `[JsonPropertyName]` attributes
5. Find nested object structures

**Example**:
```csharp
// From CreateCustomerRequest.cs
public class CreateCustomerRequest
{
    [Required]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    public string CustomerId { get; set; } = string.Empty;  // ⚠️ Not in test!
    
    public DateTimeOffset BirthDate { get; set; }  // ⚠️ Required but optional in test!
}
```

### Step 3: Compare Field-by-Field

Create comparison table for each request/response pair:

| Field | E2E Test | API Model | Match? | Issue |
|-------|----------|-----------|---------|-------|
| firstName | Required string | [Required] string FirstName | ❌ | Casing difference |
| customerId | Not sent | [Required] string CustomerId | ❌ | Test doesn't provide, should auto-generate |
| birthDate | Optional DateTimeOffset? | Required DateTimeOffset | ❌ | Optionality mismatch |
| address | Required nested object | Missing | ❌ | Test sends nested, API expects ? |

### Step 4: Check Controller Implementation

**Locate controller:**
```powershell
Get-ChildItem services/customer/src/Api/Controllers/*Controller.cs
```

**Verify**:
1. Action method accepts correct request type
2. Response type matches test expectations
3. Status codes align (201 Created vs 200 OK)
4. Auto-generated fields are created in controller

**Example check**:
```csharp
[HttpPost]
public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request)
{
    // ✅ Check: Does controller auto-generate fields test doesn't send?
    var customerId = $"CUST-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    
    // ✅ Check: Does it extract nested objects correctly?
    var address = new Address { 
        Street = request.Address.Street,  // Matches test's nested structure
        ...
    };
    
    // ✅ Check: Return type matches test expectation
    return CreatedAtAction(..., new CustomerResponse { ... });  // 201 Created ✅
}
```

### Step 5: Check Manager Interface

**Locate manager:**
```powershell
Get-ChildItem services/customer/src/Domain/Managers/*Manager.cs
```

**Verify**:
1. Method signature matches what controller needs
2. Parameters align with request model properties
3. Return type supports response model

**Example**:
```csharp
public interface ICustomerManager
{
    // ❌ Old signature required customerId from request
    Task<Customer> CreateCustomerAsync(string customerId, string email, ...);
    
    // ✅ New signature matches test: customerId auto-generated, accepts firstName/lastName
    Task<Customer> CreateCustomerAsync(string firstName, string lastName, string email, ...);
}
```

### Step 6: Generate Fixes

For each mismatch, generate specific code fixes:

#### Fix Type 1: Remove Required Field from Request (Auto-Generate Instead)

**Before**:
```csharp
public class CreateCustomerRequest
{
    [Required]
    public string CustomerId { get; set; }  // Test doesn't send this
}
```

**After**:
```csharp
public class CreateCustomerRequest
{
    // CustomerId removed - will be auto-generated in controller ✅
}

// In controller:
var customerId = $"CUST-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
```

#### Fix Type 2: Make Field Optional to Match Test

**Before**:
```csharp
[Required]
public DateTimeOffset BirthDate { get; set; }
```

**After**:
```csharp
public DateTimeOffset? BirthDate { get; set; }  // Now optional, matches test ✅
```

#### Fix Type 3: Add Nested Object Model

**Before**:
```csharp
public string Street { get; set; }
public string City { get; set; }
public string ZipCode { get; set; }
```

**After**:
```csharp
public class AddressRequest
{
    [Required]
    public string Street { get; set; } = string.Empty;
    [Required]
    public string City { get; set; } = string.Empty;
    [Required]
    public string State { get; set; } = string.Empty;
    [Required]
    public string ZipCode { get; set; } = string.Empty;
}

public class CreateCustomerRequest
{
    [Required]
    public AddressRequest Address { get; set; } = new();  // Matches test nesting ✅
}
```

#### Fix Type 4: Add JSON Property Mapping

**Before**:
```csharp
public string PhoneNumber { get; set; }  // Test sends "phone"
```

**After**:
```csharp
using System.Text.Json.Serialization;

[JsonPropertyName("phone")]  // Maps test's "phone" to PhoneNumber ✅
public string PhoneNumber { get; set; }
```

### Step 7: Update Manager Signature

If controller changes break manager interface:

**Before**:
```csharp
await _manager.CreateCustomerAsync(
    request.CustomerId,  // ❌ No longer in request
    request.Email,
    request.BirthDate,
    request.ZipCode);
```

**After**:
```csharp
var customerId = $"CUST-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

var address = new Address {
    Street = request.Address.Street,
    City = request.Address.City,
    State = request.Address.State,
    ZipCode = request.Address.ZipCode
};

await _manager.CreateCustomerAsync(
    request.FirstName,      // ✅ New parameter
    request.LastName,       // ✅ New parameter
    request.Email,
    request.Phone,          // ✅ New parameter
    address,                // ✅ New parameter (nested object)
    request.BirthDate);     // ✅ Now optional
```

Update manager interface and implementation:
```csharp
public interface ICustomerManager
{
    Task<Customer> CreateCustomerAsync(
        string firstName,
        string lastName,
        string email,
        string phoneNumber,
        Address mailingAddress,
        DateTimeOffset? birthDate = null);
}
```

### Step 8: Validate Fixes

**Build affected projects:**
```powershell
dotnet build services/customer/src/Api/Api.csproj
dotnet build services/customer/src/Domain/Domain.csproj
dotnet build services/customer/src/Infrastructure/Infrastructure.csproj
```

**Expected**:
- ✅ Build succeeded with 0 errors
- ⚠️ Warnings acceptable if not breaking
- ❌ Errors require additional fixes

**Check for breaking changes**:
```powershell
# Look for other code referencing changed manager methods
git grep "CreateCustomerAsync" -- "*.cs"
```

### Step 9: Optionally Test Changes

**Rebuild Docker image:**
```powershell
wsl docker-compose build customer-api
wsl docker-compose up -d customer-api
```

**Test with curl or PowerShell:**
```powershell
$body = @{
    firstName = "Test"
    lastName = "User"
    email = "test@example.com"
    phone = "555-0100"
    address = @{
        street = "123 Main St"
        city = "TestCity"
        state = "CA"
        zipCode = "90210"
    }
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://127.0.0.1:7073/api/customers" `
    -Method POST -Body $body -ContentType "application/json"
```

**Or run E2E tests:**
```powershell
cd test/e2e
npm test
```

---

## Report Format

### Per-Domain Section

```markdown
## Customer Domain

### Files Checked
- E2E Helper: `test/e2e/helpers/customer-api.ts`
- API Models: `services/customer/src/Api/Models/CreateCustomerRequest.cs`
- Controller: `services/customer/src/Api/Controllers/CustomersController.cs`
- Manager: `services/customer/src/Domain/Managers/CustomerManager.cs`

### Contract Comparison

#### CreateCustomer Endpoint

**Request Contract**:
| Field | E2E Test Type | API Model | Status |
|-------|---------------|-----------|--------|
| firstName | string (required) | string FirstName [Required] | ✅ Match |
| lastName | string (required) | string LastName [Required] | ✅ Match |
| email | string (required) | string Email [Required] | ✅ Match |
| phone | string (required) | string PhoneNumber [Required] | ⚠️ Name mismatch |
| address | nested object (required) | Missing | ❌ Structure mismatch |
| birthDate | DateTimeOffset (optional) | DateTimeOffset [Required] | ❌ Optionality mismatch |
| customerId | Not sent | string CustomerId [Required] | ❌ Should auto-generate |

**Issues Found**: 4

**Fixes Applied**:
1. ✅ Removed `CustomerId` from request, auto-generate in controller
2. ✅ Made `BirthDate` optional (`DateTimeOffset?`)
3. ✅ Added `AddressRequest` nested model
4. ✅ Added `[JsonPropertyName("phone")]` to `PhoneNumber`
5. ✅ Updated manager signature to accept new parameters

**Build Status**: ✅ Success

**Test Result**: ✅ Manual curl test passed (customer created successfully)
```

### Summary Section

```markdown
## Verification Summary

### Domains Processed
1. ✅ **Customer**: 4 issues fixed, builds successfully
2. ⚠️ **RatingAndUnderwriting**: 3 issues found, 2 fixed, 1 design decision needed
3. ✅ **Policy**: No issues found
4. ❓ **Billing**: No E2E test helper found
5. ❓ **FundsTransfer**: No E2E test helper found

### Overall Status
- **Aligned Domains**: 2/3 with tests
- **Requiring Attention**: 1 domain (Rating - design decision on zipCode parameter)
- **Missing E2E Coverage**: 2 domains

### Next Steps
1. Rebuild Docker images for Customer and Rating domains
2. Run full E2E test suite to verify fixes
3. Resolve design question in Rating domain (zipCode in underwriting vs propertyZipCode in quote)
4. Create E2E test helpers for Billing and FundsTransfer domains
```

---

## Common Patterns to Check

### Pattern 1: Auto-Generated IDs
- ✅ Test doesn't send ID → API should auto-generate
- ❌ Test doesn't send ID → API requires it = **MISMATCH**

### Pattern 2: Optional vs Required
- ✅ Test marks field optional (`?`) → API accepts `null` or omits `[Required]`
- ❌ Test marks field optional → API has `[Required]` = **MISMATCH**

### Pattern 3: Nested Objects
- ✅ Test sends nested → API has nested model class
- ❌ Test sends nested → API has flat properties = **MISMATCH**

### Pattern 4: Field Name Casing
- ✅ Test uses camelCase → API auto-binds PascalCase (works with default settings)
- ⚠️ Test uses different name → Use `[JsonPropertyName]` for explicit mapping

### Pattern 5: Response Structure
- ✅ Test expects nested response → API returns nested object
- ❌ Test expects nested → API returns flat = **MISMATCH**

---

## Domains to Verify

Run this verification for:

1. **Customer** (`customer-api.ts`)
2. **RatingAndUnderwriting** (`rating-api.ts`)
3. **Policy** (`policy-api.ts`)
4. **Billing** (`billing-api.ts` if exists)
5. **FundsTransferMgt** (`fundstransfer-api.ts` if exists)

For each domain, follow all 9 steps and generate detailed report.
