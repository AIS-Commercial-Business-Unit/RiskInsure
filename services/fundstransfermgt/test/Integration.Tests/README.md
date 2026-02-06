# Integration Tests

This directory contains Playwright-based integration tests for the Fund Transfer Management API.

## Test Suites

### 1. Payment Method Lifecycle Tests (`payment-method-lifecycle.spec.ts`)
Tests the complete workflow for managing payment methods including:
- Adding credit cards and ACH accounts
- Validating payment methods
- Retrieving and listing payment methods
- Removing payment methods
- Input validation and error handling

### 2. Fund Transfer End-to-End Tests (`fund-transfer-e2e.spec.ts`)
Comprehensive tests covering the complete fund transfer workflow:
- Creating payment methods (credit card and ACH)
- Initiating fund transfers
- Verifying transfer status and settlement
- Retrieving transfer history
- Multi-payment method scenarios
- Error handling (invalid/inactive payment methods, cross-customer validation)
- Large amount transfers

## Prerequisites

- Node.js 18+ installed
- Fund Transfer Management API running on `http://localhost:7073`
- Cosmos DB Emulator running locally

## First-Time Setup

```bash
# Navigate to the Integration.Tests directory
cd services/fundstransfermgt/test/Integration.Tests

# Install Node.js dependencies (creates node_modules folder)
npm install

# Install Playwright browsers (first time only)
npx playwright install

# Optional: Install only Chromium to save space
npx playwright install chromium
```

## Before Running Tests

**Start the Fund Transfer Management API:**
```bash
# In a separate terminal, navigate to the API project
cd services/fundstransfermgt/src/Api

# Run the API
dotnet run
```

The API should start on `http://localhost:7073`. Wait for the message:
```
Now listening on: http://localhost:7073
```

## Running Tests

### Quick Start
```bash
# Make sure you're in the Integration.Tests directory
cd services/fundstransfermgt/test/Integration.Tests

# Run all tests (headless - runs in background)
npm test
```

### Test Execution Options

```bash
# Run all tests (headless - fastest, no UI)
npm test

# Run tests with browser visible (see what's happening)
npm run test:headed

# Run tests in debug mode (step through each action with Inspector)
npm run test:debug

# Run tests with UI mode (interactive test explorer - RECOMMENDED for development)
npm run test:ui

# Run a specific test file
npx playwright test tests/payment-method-lifecycle.spec.ts
npx playwright test tests/fund-transfer-e2e.spec.ts

# Run specific test suites
npm run test:payment-methods     # Payment method lifecycle tests
npm run test:e2e                 # Fund transfer E2E tests
npm run test:e2e:ui             # Fund transfer E2E tests in UI mode

# Run tests matching a pattern
npx playwright test --grep "payment"
npx playwright test --grep "credit card"

# Run a single test by name
npx playwright test --grep "Complete credit card fund transfer workflow"
```

### Viewing Test Results

```bash
# View HTML test report (generated after test run)
npm run test:report

# Report shows:
# - Test pass/fail status
# - Request/response details
# - Execution time
# - Error screenshots (if any)
```

### Recommended Workflow

1. **Start API**: `cd ../../src/Api && dotnet run`
2. **Open UI Mode**: `npm run test:ui` (in Integration.Tests folder)
3. **Select tests** to run in the Playwright UI
4. **Watch tests execute** with visual feedback

## Troubleshooting

### Tests Fail with Connection Error
```
Error: connect ECONNREFUSED 127.0.0.1:7075
```
**Solution:**
- Ensure API is running: `dotnet run --project ../../src/Api`
- Check API is accessible: Open `http://localhost:7075/api/health` in browser
- Verify no other process is using port 7075

### Random Test Failures
**Possible causes:**
- Port conflicts (port 7075 in use by another process)
- Cosmos DB Emulator not running
- Previous test data not cleaned up
- Network timeout issues

**Solutions:**
- Check for port conflicts: `netstat -ano | findstr :7075` (Windows)
- Start Cosmos DB Emulator
- Check API logs for errors in the terminal
- Add retry configuration in `playwright.config.ts`

### Tests Pass Locally but Fail in CI
- Check environment variables (API_BASE_URL)
- Verify database/dependencies are available in CI
- Review CI logs for API startup errors
- Ensure test data is unique (using UUIDs, timestamps)

### Debug a Specific Failing Test
```bash
# Step through test with Inspector
npx playwright test --debug -g "Add credit card"

# Run with verbose logging
npx playwright test --reporter=line -g "payment"

# Generate trace for debugging
npx playwright test --trace on
npx playwright show-trace test-results/.../trace.zip
```

### View Request/Response Details
Add logging to your test:
```typescript
const response = await request.post('/endpoint', { data: {...} });
console.log('Status:', response.status());
console.log('Body:', await response.json());
```

### Clean Up Test Data
If tests leave behind data in Cosmos DB:
- Restart Cosmos DB Emulator
- Or manually delete test documents through Azure Storage Explorer

## Environment Variables

- `API_BASE_URL` - Base URL for the API (default: `http://localhost:7075/api`)

Example:
```bash
API_BASE_URL=https://staging-api.example.com/api npm test
```

## CI/CD Integration

Tests are configured to run in CI pipelines with:
- JUnit XML reports (`test-results/junit.xml`)
- HTML reports (`playwright-report/index.html`)
- Automatic retries on failure (2 retries in CI)

## Writing New Tests

See `tests/payment-method-lifecycle.spec.ts` and `tests/fund-transfer-e2e.spec.ts` for examples. Each test:
1. Generates unique test data (GUIDs, timestamps)
2. Uses `request` fixture for API calls
3. Validates responses with `expect()` assertions
4. Uses domain terminology consistently
5. Includes console logging for test execution visibility

### Example Test Structure
```typescript
test('Descriptive test name', async ({ request }) => {
  const customerId = `CUST-${Date.now()}`;
  
  // Step 1: Setup
  console.log('📋 STEP 1: Creating payment method...');
  const response = await request.post(`${baseUrl}/payment-methods/credit-card`, {
    data: { /* ... */ }
  });
  
  // Step 2: Validate
  expect(response.status()).toBe(201);
  const result = await response.json();
  expect(result.customerId).toBe(customerId);
  
  console.log('✅ Test completed successfully!');
});
```

## Test Coverage

### Payment Method Lifecycle Tests
- ✅ Add credit card with full validation
- ✅ Add ACH account
- ✅ Retrieve payment method by ID
- ✅ List customer payment methods
- ✅ Remove payment method
- ✅ Invalid card number validation
- ✅ Input validation for required fields

### Fund Transfer End-to-End Tests
- ✅ Complete credit card transfer workflow
- ✅ Complete ACH transfer workflow
- ✅ Transfer with invalid payment method (404 handling)
- ✅ Transfer with inactive payment method (400 handling)
- ✅ Transfer with wrong customer's payment method (authorization)
- ✅ Multiple payment methods for one customer
- ✅ Multiple transfers for one customer
- ✅ Large amount transfers ($10,000+)
- ✅ Retrieve transfer by ID
- ✅ Retrieve customer transfer history
- ✅ Non-existent transfer returns 404

Integration tests should cover:
- Payment method management (credit cards, ACH accounts)
- Fund transfer workflows
- Refund processing
- Validation scenarios (negative amounts, invalid data)
- Error handling and status codes
