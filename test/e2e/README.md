# RiskInsure E2E Integration Tests

**End-to-end tests for cross-domain workflows** spanning Customer, Rating & Underwriting, Policy, Billing, and Funds Transfer Management domains.

---

## Overview

These tests verify **complete business flows** that cross multiple bounded context boundaries, ensuring that event-driven integrations work correctly. Unlike domain-specific integration tests, these tests assume all services are running and verify the entire user journey.

### What Gets Tested

✅ **Quote-to-Policy Flow**: Customer creation → Quote → Underwriting → Accept → Policy created  
✅ **Eventual Consistency**: Verifies async events (QuoteAccepted → PolicyCreated)  
✅ **Cross-Domain Data Integrity**: Policy details match quote details  
✅ **Business Rules End-to-End**: Class A/B approvals, declines, validation

---

## Quick Start

### Prerequisites

**All APIs must be running**:
- Customer API: `http://127.0.0.1:7073`
- Rating & Underwriting API: `http://127.0.0.1:7079`
- Policy API: `http://127.0.0.1:7077`
- Billing API: `http://127.0.0.1:7071`
- Funds Transfer API: `http://127.0.0.1:7075`

> **Note**: Tests use `127.0.0.1` instead of `localhost` to force IPv4 resolution (Playwright on Windows may prefer IPv6 `::1` which can cause connection issues).

### First-Time Setup

```powershell
# Navigate to E2E test directory
cd test/e2e

# Install dependencies
npm install

# Install Playwright browsers
npx playwright install chromium

# Copy environment template (optional - defaults work for local)
cp .env.example .env
```

### Run Tests

```powershell
# Run all E2E tests (headless)
npm test

# Interactive UI mode (recommended for development)
npm run test:ui

# Headed mode (see browser)
npm run test:headed

# Debug mode (step-through)
npm run test:debug

# View last test report
npm run test:report
```

---

## 🤖 Copilot-Assisted Debugging

### Automatic Diagnostics Capture

When tests fail, use the **diagnostic runner** to automatically capture everything needed for Copilot analysis:

```powershell
# Run tests with automatic diagnostics
.\run-with-diagnostics.ps1

# Run specific test with diagnostics
.\run-with-diagnostics.ps1 -Test "quote-to-policy"

# Interactive UI mode
.\run-with-diagnostics.ps1 -UI
```

### What Gets Captured

When tests fail, the script automatically creates a timestamped folder with:

✅ **Test Results**: JSON and HTML reports  
✅ **API Logs**: Last 200 lines from each service container  
✅ **Network Traces**: Playwright HAR files (if enabled)  
✅ **Screenshots**: Visual snapshots of failures  
✅ **Copilot Prompt**: Pre-formatted analysis request

### Workflow

1. **Run Tests**:
   ```powershell
   .\run-with-diagnostics.ps1
   ```

2. **If Tests Fail**:
   - Diagnostics folder opens automatically
   - Open `COPILOT-ANALYSIS.txt`
   - Copy the Copilot prompt section

3. **Paste in Copilot**:
   ```
   @workspace Analyze e2e test failure
   
   [Copilot will read test results and API logs]
   ```

4. **Copilot Will**:
   - Read test results from JSON
   - Review API logs for errors
   - Check relevant API code
   - Suggest and implement fixes

### Manual Diagnostics

If you need to manually gather context:

```powershell
# Get specific API logs
wsl docker logs riskinsure-policy-api-1 --tail 200

# Check all service status
wsl docker ps --filter "name=riskinsure"

# View test trace
npx playwright show-trace test-results/.../trace.zip
```

### Tips for Better Results

- **Run one test at a time** when debugging: `-Test "quote-to-policy"`
- **Use UI mode** for interactive debugging: `npm run test:ui`
- **Check API logs first** - most failures are API-side errors
- **Verify services running**: `.\scripts\smoke-test.ps1` before testing
- **Include test context** when asking Copilot (test name, expected vs actual)

---

## Configuration

### Environment Variables

Configure API endpoints via environment variables or `.env` file:

```bash
# API Base URLs (use 127.0.0.1 for IPv4 - not localhost)
CUSTOMER_API_URL=http://127.0.0.1:7073
RATING_API_URL=http://127.0.0.1:7079
POLICY_API_URL=http://127.0.0.1:7077
BILLING_API_URL=http://127.0.0.1:7071
FUNDS_TRANSFER_API_URL=http://127.0.0.1:7075

# Test Timeouts (milliseconds)
EVENTUAL_CONSISTENCY_TIMEOUT=10000
API_REQUEST_TIMEOUT=30000

# Test Data Prefixes
TEST_CUSTOMER_PREFIX=E2E-TEST-CUST-
TEST_QUOTE_PREFIX=E2E-TEST-QUOTE-
```

### CI/CD Pipeline Configuration

#### Azure DevOps Pipeline Example

```yaml
# azure-pipelines.yml
- task: Bash@3
  displayName: 'Run E2E Tests'
  env:
    CUSTOMER_API_URL: https://riskinsure-customer-api-dev.azurewebsites.net
    RATING_API_URL: https://riskinsure-rating-api-dev.azurewebsites.net
    POLICY_API_URL: https://riskinsure-policy-api-dev.azurewebsites.net
    BILLING_API_URL: https://riskinsure-billing-api-dev.azurewebsites.net
    FUNDS_TRANSFER_API_URL: https://riskinsure-funds-api-dev.azurewebsites.net
    EVENTUAL_CONSISTENCY_TIMEOUT: 15000
  inputs:
    targetType: 'inline'
    script: |
      cd test/e2e
      npm ci
      npx playwright install --with-deps chromium
      npm test
    workingDirectory: '$(Build.SourcesDirectory)'

- task: PublishTestResults@2
  displayName: 'Publish E2E Test Results'
  inputs:
    testResultsFormat: 'JUnit'
    testResultsFiles: '**/test-results.json'
    failTaskOnFailedTests: true
```

#### GitHub Actions Example

```yaml
# .github/workflows/e2e-tests.yml
name: E2E Tests

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  e2e-tests:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup Node.js
      uses: actions/setup-node@v3
      with:
        node-version: '20'
    
    - name: Install dependencies
      working-directory: test/e2e
      run: npm ci
    
    - name: Install Playwright
      working-directory: test/e2e
      run: npx playwright install --with-deps chromium
    
    - name: Run E2E Tests
      working-directory: test/e2e
      env:
        CUSTOMER_API_URL: ${{ secrets.DEV_CUSTOMER_API_URL }}
        RATING_API_URL: ${{ secrets.DEV_RATING_API_URL }}
        POLICY_API_URL: ${{ secrets.DEV_POLICY_API_URL }}
        BILLING_API_URL: ${{ secrets.DEV_BILLING_API_URL }}
        FUNDS_TRANSFER_API_URL: ${{ secrets.DEV_FUNDS_API_URL }}
      run: npm test
    
    - name: Upload test results
      if: always()
      uses: actions/upload-artifact@v3
      with:
        name: playwright-report
        path: test/e2e/playwright-report/
```

---

## Test Structure

### Directory Layout

```
test/e2e/
├── config/
│   └── api-endpoints.ts          # Centralized API configuration
├── helpers/
│   ├── customer-api.ts            # Customer domain API helpers
│   ├── rating-api.ts              # Rating & Underwriting API helpers
│   ├── policy-api.ts              # Policy domain API helpers (+ wait helper)
│   ├── billing-api.ts             # Billing domain API helpers (future)
│   └── funds-transfer-api.ts      # Funds Transfer API helpers (future)
├── tests/
│   ├── quote-to-policy-flow.spec.ts      # Quote → Policy workflow
│   ├── payment-collection-flow.spec.ts    # Policy → Payment (future)
│   └── cancellation-refund-flow.spec.ts   # Cancel → Refund (future)
├── package.json
├── playwright.config.ts
├── .env.example
└── README.md
```

### Helper Functions

Each domain has a helper module with reusable API functions:

```typescript
// helpers/customer-api.ts
export async function createCustomer(request, data?) => Customer
export async function getCustomer(request, customerId) => Customer

// helpers/rating-api.ts
export async function startQuote(request, customerId, data?) => Quote
export async function submitUnderwriting(request, quoteId, data?) => UnderwritingResponse
export async function acceptQuote(request, quoteId) => AcceptQuoteResponse
export async function getQuote(request, quoteId) => Quote

// helpers/policy-api.ts
export async function getPolicy(request, policyId) => Policy
export async function getCustomerPolicies(request, customerId) => Policy[]
export async function waitForPolicyCreation(request, customerId, timeout?) => Policy
```

### Eventual Consistency Pattern

The `waitForPolicyCreation()` helper handles async event processing:

```typescript
// Polls Policy API until policy is created (handles QuoteAccepted → PolicyCreated)
const policy = await waitForPolicyCreation(request, customer.customerId);
// Default: 10 seconds timeout, 500ms poll interval
// Configurable via EVENTUAL_CONSISTENCY_TIMEOUT env var
```

---

## Test Scenarios

### Quote-to-Policy Flow (Current)

**Test**: `quote-to-policy-flow.spec.ts`

1. ✅ **Class A Approval**: 0 claims, age ≤15, Excellent credit → Approved → Policy created
2. ✅ **Class B Approval**: 1 claim, age ≤30, Good credit → Approved → Policy created
3. ✅ **Declined Quote**: 3+ claims → Declined → No policy created

**Verifies**:
- Customer creation
- Quote lifecycle (Draft → UnderwritingPending → Quoted → Accepted)
- Underwriting business rules
- Premium calculation
- QuoteAccepted event triggers policy creation (eventual consistency)
- Policy details match quote details
- Policy status is "Bound"

### Future Flows

**Payment Collection Flow** (TODO):
1. Policy created → Payment instruction generated
2. Funds collected from customer
3. Policy status updated to "Active"

**Cancellation & Refund Flow** (TODO):
1. Policy cancellation request
2. Refund calculation (prorated premium)
3. Funds returned to customer
4. Policy status "Cancelled"

---

## Test Data Management

### Strategy

- **No Cleanup**: Tests leave data in the database
- **Unique IDs**: Each test creates unique customers (timestamp-based emails)
- **Test Prefixes**: All test data uses `E2E-TEST-` prefix for easy identification
- **Dev Environment Only**: Tests run against dev Cosmos DB

### Identifying Test Data

All E2E test customers have emails like:
```
E2E-TEST-CUST-1738876543210@example.com
```

To clean up test data manually (if needed):
```sql
-- Cosmos DB query to find E2E test customers
SELECT * FROM c WHERE CONTAINS(c.email, 'E2E-TEST-CUST-')
```

---

## Troubleshooting

### "Policy not created within 10000ms"

**Cause**: Eventual consistency timeout - Policy domain didn't create policy in time.

**Solutions**:
1. Check Policy Endpoint.In is running and processing messages
2. Check RabbitMQ/NServiceBus flow for `QuoteAccepted` events
3. Increase timeout: `EVENTUAL_CONSISTENCY_TIMEOUT=20000`
4. Check Cosmos DB manually for policy documents

### "Connection refused" or 404 errors

**Cause**: API not running on expected port.

**Solutions**:
1. Verify all APIs are running (see Prerequisites)
2. Check port configuration in `.env` or environment variables
3. Verify API base URLs: `curl http://127.0.0.1:7073/api/customers` should return 404 (not connection error)
4. **Windows/Playwright**: If seeing `ECONNREFUSED ::1`, you have IPv6 issue - use `127.0.0.1` not `localhost`

### "Network request failed" in CI/CD

**Cause**: APIs not accessible from CI/CD agent.

**Solutions**:
1. Ensure APIs are deployed and accessible from agent
2. Check firewall rules allow CI/CD agent IP
3. Verify environment variables are set correctly in pipeline
4. Use Azure-hosted agents with VNET access if needed

### Tests pass locally but fail in pipeline

**Cause**: Environment differences (timeouts, API URLs, network latency).

**Solutions**:
1. Increase timeouts in pipeline: `EVENTUAL_CONSISTENCY_TIMEOUT=20000`
2. Add retry logic: `retries: 2` in playwright.config.ts
3. Check pipeline logs for specific error messages
4. Verify RabbitMQ and Cosmos DB are accessible from pipeline

---

## Performance Considerations

### Test Duration

- **Single test**: ~5-15 seconds (includes eventual consistency wait)
- **Full suite**: ~1-2 minutes (3 tests currently)
- **CI/CD**: Add 30-60s for npm install and Playwright setup

### Optimization Tips

1. **Run sequentially**: Tests use `fullyParallel: false` to avoid state conflicts
2. **Reuse customers**: (Future) Share customer across tests to reduce API calls
3. **Mock external services**: (Future) Mock payment gateways, email services
4. **Containerize APIs**: (Future) Use Docker Compose for faster startup

---

## Adding New E2E Tests

### Step 1: Create Helper Functions

```typescript
// helpers/billing-api.ts
export async function createInvoice(request: APIRequestContext, data) {
  const response = await request.post(
    `${config.apis.billing}/api/invoices`,
    { data, timeout: config.timeouts.apiRequest }
  );
  expect(response.status()).toBe(201);
  return await response.json();
}
```

### Step 2: Create Test File

```typescript
// tests/new-flow.spec.ts
import { test, expect } from '@playwright/test';
import { getTestConfig, validateConfig } from '../config/api-endpoints';
import { createCustomer } from '../helpers/customer-api';
import { createInvoice } from '../helpers/billing-api';

test.describe('New Business Flow', () => {
  test.beforeAll(() => {
    validateConfig(getTestConfig());
  });

  test('should complete new workflow', async ({ request }) => {
    // 1. Setup
    const customer = await createCustomer(request);
    
    // 2. Execute workflow
    const invoice = await createInvoice(request, { customerId: customer.customerId });
    
    // 3. Verify
    expect(invoice.customerId).toBe(customer.customerId);
  });
});
```

### Step 3: Update Configuration (if needed)

Add new API endpoint to `config/api-endpoints.ts`:

```typescript
export interface ApiEndpoints {
  // ... existing
  newService: string;
}

export function getTestConfig(): TestConfig {
  return {
    apis: {
      // ... existing
      newService: process.env.NEW_SERVICE_API_URL || 'http://localhost:7081',
    },
    // ...
  };
}
```

---

## Best Practices

### ✅ Do

- **Use helper functions** - Keep tests readable, logic in helpers
- **Validate config** - Call `validateConfig()` in `beforeAll()`
- **Handle eventual consistency** - Use `waitFor*()` helpers for async flows
- **Log test progress** - Use `console.log()` to show flow steps
- **Verify end state** - Assert final state, not just intermediate steps
- **Use descriptive names** - Test names should explain the scenario
- **Set timeouts appropriately** - Balance speed vs. reliability

### ❌ Don't

- **Don't hardcode URLs** - Use `config.apis.*` from configuration
- **Don't use fixed delays** - Use polling helpers instead of `sleep()`
- **Don't share state** - Each test creates own data
- **Don't test domain logic** - E2E tests verify integration, not algorithms
- **Don't ignore failures** - Investigate root cause, don't just increase timeouts
- **Don't commit secrets** - Use environment variables for sensitive data

---

## Continuous Improvement

### Adding Auto-Start (Future)

To auto-start APIs before tests:

```typescript
// playwright.config.ts
export default defineConfig({
  webServer: [
    { command: 'dotnet run', cwd: '../services/customer/src/Api', port: 7073 },
    { command: 'dotnet run', cwd: '../services/rating/src/Api', port: 7079 },
    { command: 'dotnet run', cwd: '../services/policy/src/Api', port: 7077 },
    // ... etc
  ],
  // ...
});
```

**Challenges**:
- Multiple services slow startup
- Cosmos DB/RabbitMQ dependencies
- Better handled by Docker Compose

---

## Support

**Documentation**: See [Architecture Constitution](../../.specify/memory/constitution.md)  
**Domain Tests**: Each service has its own integration tests in `services/{domain}/test/Integration.Tests`  
**Issues**: File issues with `[E2E]` prefix for cross-domain test failures

---

**Version**: 1.0.0  
**Last Updated**: February 6, 2026  
**Status**: ✅ **READY FOR TESTING**
