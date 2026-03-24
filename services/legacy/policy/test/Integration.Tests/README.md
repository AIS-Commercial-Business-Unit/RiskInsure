# Policy Integration Tests

End-to-end API integration tests using Playwright.

## Prerequisites

- Node.js 18+ installed
- Policy API running on http://localhost:7077
- Cosmos DB Emulator running
- RabbitMQ connection configured

## Setup

```powershell
# Install dependencies
npm install

# Install Playwright browsers
npx playwright install chromium
```

## Running Tests

```powershell
# Run all tests (headless)
npm test

# Run tests with UI (recommended for development)
npm run test:ui

# Run tests with browser visible
npm run test:headed

# Debug tests (step through)
npm run test:debug

# View last test report
npm run test:report
```

## Test Scenarios

### Testing Scope and Limitations

**IMPORTANT**: This domain's integration tests are **limited to scenarios the domain fully controls**.

**Why?**: Policy domain has **no direct policy creation mechanism**. Policies are created only via **QuoteAccepted events** published by the Rating & Underwriting domain (which is not yet implemented). Therefore, these integration tests cannot test "happy path" scenarios requiring existing policies.

**What We Test** (Domain-Controlled Scenarios):
- ✅ **404 errors** for non-existent entities (domain controls error response)
- ✅ **Validation errors** on malformed requests (domain controls validation)
- ✅ **Empty collection responses** when no data exists (domain controls empty response)

**What We Defer to Enterprise Integration Tests** (Cross-Domain Scenarios):
- ❌ Issuing bound policies (requires existing policy in Bound status from QuoteAccepted event)
- ❌ Cancelling active policies (requires existing policy in Active status)
- ❌ Reinstating cancelled policies (requires existing policy in Cancelled status)
- ❌ Retrieving existing policies (requires policy created via QuoteAccepted event)
- ❌ Listing customer policies with data (requires policies from QuoteAccepted events)

These "happy path" tests will be implemented in **enterprise integration tests** once Rating & Underwriting domain exists and can publish QuoteAccepted events.

### Issue Policy (`tests/issue-policy.spec.ts`)
- ✅ Return 404 when policy not found

### Get Policy (`tests/get-policy.spec.ts`)
- ✅ Return 404 when policy not found

### Cancel Policy (`tests/cancel-policy.spec.ts`)
- ✅ Return 404 when policy not found
- ✅ Validate required fields (cancellationReason, cancellationDate)

### Reinstate Policy (`tests/reinstate-policy.spec.ts`)
- ✅ Return 404 when policy not found

### Get Customer Policies (`tests/customer-policies.spec.ts`)
- ✅ Return empty array when customer has no policies

## Important Notes

### Cross-Domain Testing Philosophy

**Pattern**: Domain integration tests should ONLY test what the domain directly controls. Tests requiring data from other domains (via events, external APIs, etc.) belong in **enterprise integration tests**.

**Policy Domain Example**:
- Policy creation requires **QuoteAccepted event** from Rating & Underwriting domain
- Rating & Underwriting domain is not yet implemented
- Therefore, Policy domain integration tests cannot create test data
- Tests focus on error scenarios (404, validation) that don't require existing data
- "Happy path" tests deferred until Rating & Underwriting exists

**General Principle for All Domains**:
- If domain can create entities via direct API call → Test full CRUD in integration tests
- If domain creates entities via events from other domains → Test only error scenarios in integration tests
- Cross-domain scenarios → Defer to enterprise integration tests

### Validation Error Assertions

ASP.NET Core returns **ProblemDetails** format for model validation errors:

```typescript
// CORRECT - ASP.NET ProblemDetails format
expect(error.status).toBe(400);
expect(error.errors.FieldName).toBeDefined();
expect(Array.isArray(error.errors.FieldName)).toBe(true);

// INCORRECT - Custom error format (only for business validation)
expect(error.error).toBe('ValidationFailed');
expect(error.message).toBeDefined();
```

**Business validation errors** (InvalidPolicyStatus, PolicyNotFound) use custom format:
```typescript
expect(error.error).toBe('InvalidPolicyStatus');
expect(error.message).toContain('expected text');
```

## Configuration

Edit `playwright.config.ts` to change:
- Base URL (default: http://localhost:7077)
- Number of workers (default: 1 for sequential execution)
- Retries (default: 0 for dev, 2 for CI)

## CI/CD

Set `CI=true` environment variable to enable:
- 2 retries on failure
- Strict mode (fail on `.only` usage)

## Troubleshooting

**Tests fail with connection refused**:
- Ensure API is running: `cd services/policy/src/Api && dotnet run`
- Check port 7077 is available

**Tests fail with 500 errors**:
- Check Cosmos DB Emulator is running
- Verify connection strings in `appsettings.Development.json`
- Review API logs for errors

**Validation error tests fail**:
- Verify API returns correct error format
- Test one endpoint manually first
- Check ASP.NET Core model validation configuration
