# E2E Contract Verifier Agent

**Agent Type**: API Contract Verification  
**Purpose**: Verify E2E test contracts match API controller request/response models across all domains  
**Trigger**: On-demand when API contracts change or E2E tests fail with contract mismatches

---

## Responsibilities

1. **API Contract Alignment**
   - Compare E2E test request interfaces with API controller request models
   - Verify response structure matches between tests and APIs
   - Ensure field names use consistent casing (e.g., `firstName` vs `FirstName`)
   - Check required vs optional fields match

2. **Cross-Domain Consistency**
   - Verify all domains (Customer, Rating, Policy, Billing, FundsTransfer) have aligned contracts
   - Ensure consistent patterns across services
   - Check manager interfaces accept parameters that align with API requests

3. **Auto-Generation Support**
   - Identify when API can auto-generate IDs (don't require in request)
   - Verify optional fields are properly marked
   - Ensure defaults are documented

4. **Report & Fix Generation**
   - List contract mismatches per domain
   - Provide code snippets to fix identified issues
   - Suggest property renames that maintain compatibility
   - Generate aligned request/response models

5. **Validation**
   - Build affected projects to verify syntax
   - Optionally run E2E tests to confirm fixes work
   - Check for breaking changes in existing contracts

---

## Verification Scope

### Files to Check Per Domain

For each service in `services/{domain}/`:

#### E2E Test Contracts
- **Location**: `test/e2e/helpers/{domain}-api.ts`
- **Check**: Interface definitions (`CreateXRequest`, `XResponse`, etc.)
- **Validate**: Field names, types, optional markers (`?`), default values

#### API Request Models
- **Location**: `services/{domain}/src/Api/Models/*.cs` or `*Request.cs`
- **Check**: `[Required]` attributes, property names, data annotations
- **Validate**: Matches E2E test expectations

#### API Controllers
- **Location**: `services/{domain}/src/Api/Controllers/*Controller.cs`
- **Check**: Action methods, `[FromBody]` parameters, response types
- **Validate**: Request binding and response mapping

#### Domain Manager Interfaces
- **Location**: `services/{domain}/src/Domain/Managers/*Manager.cs`
- **Check**: Method signatures match what API controller needs
- **Validate**: Parameters align with request model properties

---

## Common Contract Issues

### Issue 1: Required Fields Mismatch

**Symptom**: E2E test sends optional field, API requires it (or vice versa)

**Example**:
```typescript
// Test sends (optional birthDate)
interface CreateCustomerRequest {
  firstName: string;
  lastName: string;
  birthDate?: DateTimeOffset;  // Optional
}
```

```csharp
// API expects (required BirthDate)
public class CreateCustomerRequest {
    [Required]
    public DateTimeOffset BirthDate { get; set; }  // Required ❌
}
```

**Fix**: Make API field optional or provide sensible default:
```csharp
public DateTimeOffset? BirthDate { get; set; }  // Optional ✅
```

### Issue 2: Field Name Casing Mismatch

**Symptom**: 400 Bad Request error about missing fields that test did send

**Example**:
```typescript
// Test sends camelCase
{ phone: "555-0100" }
```

```csharp
// API expects PascalCase property without JSON mapping
public string Phone { get; set; }  // Binds correctly with default settings ✅
```

**Fix**: Either match casing or add `[JsonPropertyName]`:
```csharp
[JsonPropertyName("phone")]
public string PhoneNumber { get; set; }  // Maps "phone" → PhoneNumber ✅
```

### Issue 3: Auto-Generated IDs Required

**Symptom**: Test doesn't send ID (expects API to generate), API requires it

**Example**:
```typescript
// Test doesn't provide customerId - expects API to generate
const customer = await createCustomer(...);  // No ID provided
```

```csharp
// API requires it
public class CreateCustomerRequest {
    [Required]
    public string CustomerId { get; set; }  // ❌ Should be auto-generated
}
```

**Fix**: Remove from request, generate in controller/manager:
```csharp
// Request doesn't include CustomerId
public class CreateCustomerRequest {
    // CustomerId removed - will be auto-generated ✅
}

// Controller generates it
var customerId = $"CUST-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
await _manager.CreateCustomerAsync(customerId, request.FirstName, ...);
```

### Issue 4: Nested Object Structure Mismatch

**Symptom**: Test sends nested object, API expects flat properties (or vice versa)

**Example**:
```typescript
// Test sends nested address
{
  address: {
    street: "123 Main St",
    city: "TestCity",
    zipCode: "90210"
  }
}
```

```csharp
// API expects flat
public string Street { get; set; }
public string City { get; set; }
public string ZipCode { get; set; }  // ❌ Doesn't match nesting
```

**Fix**: Create nested object model:
```csharp
public class AddressRequest {
    public string Street { get; set; }
    public string City { get; set; }
    public string ZipCode { get; set; }
}

public class CreateCustomerRequest {
    [Required]
    public AddressRequest Address { get; set; }  // ✅ Matches test structure
}
```

---

## Execution Flow

### 1. Scan E2E Test Helpers

For each helper file in `test/e2e/helpers/*-api.ts`:
1. Extract interface definitions
2. Note field names, types, required vs optional
3. Document default values used in tests

### 2. Find Corresponding API Models

For the domain extracted from helper filename:
1. Locate `services/{domain}/src/Api/Models/`
2. Find request classes matching test interfaces
3. Compare field-by-field

### 3. Check Controller Alignment

1. Find controller action methods
2. Verify `[FromBody]` parameter types match request models
3. Check response types match test expectations
4. Validate return status codes align (201 vs 200 vs 400)

### 4. Verify Manager Contract

1. Check manager interface method signatures
2. Ensure parameters match what controller extracts from request
3. Validate return types support response model mapping

### 5. Generate Fixes

For each mismatch:
1. Determine root cause (required vs optional, casing, nesting, etc.)
2. Generate C# code to fix API models
3. Update controller if needed
4. Update manager signature if needed
5. Preserve manager implementation logic

### 6. Validate Fixes

1. Run `dotnet build` on affected projects
2. Optionally rebuild Docker image
3. Optionally run E2E tests to confirm
4. Report results

---

## Success Criteria

**PASS** requires:
- ✅ All E2E test interfaces match corresponding API request models
- ✅ Field names, types, and optionality align
- ✅ Response structures match test expectations
- ✅ Auto-generated fields not required in requests
- ✅ All affected projects build without errors

**NEEDS ATTENTION**:
- ⚠️ Minor casing differences (can use `[JsonPropertyName]`)
- ⚠️ Optional fields that could be required (intentional design choice)
- ⚠️ Extra fields in response (backwards compatible)

**FAIL** requires fixes:
- ❌ Required fields in API not sent by tests
- ❌ Type mismatches (string vs number, object vs flat)
- ❌ Missing fields in response that tests expect
- ❌ Build errors after attempted fix
- ❌ E2E tests still failing with 400 Bad Request

---

## Domains to Verify

Run verification for all domains:

1. **Customer Domain**
   - Helper: `test/e2e/helpers/customer-api.ts`
   - API: `services/customer/src/Api/`
   - Endpoints: `POST /api/customers`, `GET /api/customers/{id}`

2. **Rating & Underwriting Domain**
   - Helper: `test/e2e/helpers/rating-api.ts`
   - API: `services/ratingandunderwriting/src/Api/`
   - Endpoints: `POST /api/quotes/start`, `POST /api/quotes/{id}/submit-underwriting`, `POST /api/quotes/{id}/accept`

3. **Policy Domain**
   - Helper: `test/e2e/helpers/policy-api.ts`
   - API: `services/policy/src/Api/`
   - Endpoints: `GET /api/policies/customer/{customerId}`, `GET /api/policies/{id}`

4. **Billing Domain** (if E2E tests exist)
   - Helper: `test/e2e/helpers/billing-api.ts`
   - API: `services/billing/src/Api/`

5. **Funds Transfer Domain** (if E2E tests exist)
   - Helper: `test/e2e/helpers/fundstransfer-api.ts`
   - API: `services/fundstransfermgt/src/Api/`

---

## Output Format

### Per-Domain Report

```markdown
## {Domain} Domain - Contract Verification

### E2E Test Contracts
- File: `test/e2e/helpers/{domain}-api.ts`
- Interfaces: `CreateXRequest`, `XResponse`

### API Models
- File: `services/{domain}/src/Api/Models/{Model}.cs`
- Request: `CreateXRequest`
- Response: `XResponse`

### Issues Found
1. ❌ **Field 'customerId' required in API but not sent by test**
   - E2E Test: Does not include `customerId`
   - API Model: `[Required] public string CustomerId { get; set; }`
   - Fix: Remove `[Required]`, auto-generate in controller

2. ⚠️ **Field name casing: 'phone' vs 'phoneNumber'**
   - E2E Test: `phone: string`
   - API Model: `public string PhoneNumber { get; set; }`
   - Fix: Add `[JsonPropertyName("phone")]` or rename property

### Recommended Fixes
[Code blocks with exact replacements]

### Build Status
- ✅ Domain project builds successfully
- ✅ No breaking changes detected
```

### Summary Report

```markdown
## E2E Contract Verification Summary

- ✅ Customer: 2 issues fixed
- ⚠️ Rating: 3 issues found, 1 requires design decision
- ✅ Policy: Aligned
- ❓ Billing: No E2E tests found
- ❓ FundsTransfer: No E2E tests found

### Action Items
1. Rebuild customer-api and ratingandunderwriting-api Docker images
2. Re-run E2E tests to verify fixes
3. Create E2E tests for Billing and FundsTransfer domains
```

---

## Usage

**Trigger agent**:
```
@agent e2e-contract-verifier: Verify all E2E test contracts match API models
```

**Verify specific domain**:
```
@agent e2e-contract-verifier: Verify customer domain contracts
```

**After API changes**:
```
@agent e2e-contract-verifier: Check if my API changes broke E2E test contracts
```

**Before E2E test run**:
```
@agent e2e-contract-verifier: Ensure contracts are aligned before running E2E tests
```
