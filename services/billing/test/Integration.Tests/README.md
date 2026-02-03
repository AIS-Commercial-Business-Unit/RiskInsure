# Integration Tests

This directory contains Playwright-based integration tests for the Billing API.

## Prerequisites

- Node.js 18+ installed
- Billing API running on `http://localhost:7071`
- Cosmos DB Emulator running locally

## First-Time Setup

```bash
# Navigate to the Integration.Tests directory
cd services/billing/test/Integration.Tests

# Install Node.js dependencies (creates node_modules folder)
npm install

# Install Playwright browsers (first time only)
npx playwright install

# Optional: Install only Chromium to save space
npx playwright install chromium
```

## Before Running Tests

**Start the Billing API:**
```bash
# In a separate terminal, navigate to the API project
cd services/billing/src/Api

# Run the API
dotnet run
```

The API should start on `http://localhost:7071`. Wait for the message:
```
Now listening on: http://localhost:7071
```

## Running Tests

### Quick Start
```bash
# Make sure you're in the Integration.Tests directory
cd services/billing/test/Integration.Tests

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
npx playwright test tests/billing-account-lifecycle.spec.ts

# Run tests matching a pattern
npx playwright test --grep "payment"

# Run a single test by name
npx playwright test --grep "Complete billing account workflow"
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
### Tests Fail with Connection Error
```
Error: connect ECONNREFUSED 127.0.0.1:7071
```
**Solution:**
- Ensure API is running: `dotnet run --project ../../src/Api`
- Check API is accessible: Open `http://localhost:7071/api/billing/payments/health` in browser
- Verify no other process is using port 7071

### Random Test Failures
**Possible causes:**
- Port conflicts (port 7071 in use by another process)
- Cosmos DB Emulator not running
- Previous test data not cleaned up
- Network timeout issues

**Solutions:**
- Check for port conflicts: `netstat -ano | findstr :7071` (Windows)
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
npx playwright test --debug -g "Payment validation - negative amount"

# Run with verbose logging
npx playwright test --reporter=line -g "Payment validation"

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
- Or manually delete test documents through Azure Storage ExplorerEnvironment Variables

- `API_BASE_URL` - Base URL for the API (default: `http://localhost:7071/api`)

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

See `tests/billing-account-lifecycle.spec.ts` for examples. Each test:
1. Generates unique test data (GUIDs, timestamps)
2. Uses `request` fixture for API calls
3. Validates responses with `expect()` assertions
4. Cleans up by closing accounts (optional)

## Troubleshooting

**Tests fail with connection error:**
- Ensure API is running: `dotnet run --project ../../src/Api`
- Check API is accessible at `http://localhost:7071`

**Random test failures:**
- Check for port conflicts (port 7071)
- Verify Cosmos DB Emulator is running
- Check API logs for errors

**Need to debug a specific test:**
```bash
npx playwright test --debug -g "Complete billing account workflow"
```
