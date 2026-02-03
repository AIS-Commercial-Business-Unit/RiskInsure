# Playwright Integration Testing Guide

## Overview

This document provides guidance on using **Playwright** for API integration testing in this repository. Playwright is a powerful end-to-end testing framework that excels at both UI and API testing.

**Why Playwright for API Integration Tests?**
- ✅ Simple, readable test syntax
- ✅ Built-in test runners and assertions
- ✅ Automatic retries and error handling
- ✅ Excellent CI/CD integration
- ✅ Multi-environment support
- ✅ Detailed HTML reports and traces
- ✅ TypeScript support out of the box

## When to Use Playwright

### ✅ Use Playwright For:
- **Integration Tests**: Testing API endpoints with real HTTP calls
- **End-to-End Workflows**: Complete user/business workflows across multiple APIs
- **Contract Testing**: Validating API request/response contracts
- **Cross-Service Testing**: Testing interactions between multiple services
- **Performance Smoke Tests**: Basic performance validation
- **CI/CD Pipeline Tests**: Automated testing in build pipelines

### ❌ Don't Use Playwright For:
- **Unit Tests**: Use xUnit/NUnit for isolated business logic
- **Load Testing**: Use k6, JMeter, or Apache Bench
- **Mocking External APIs**: Use WireMock or Moq
- **Database Testing**: Use Respawn or database-specific tools

## Project Structure

```
test/
├── Unit.Tests/                    # xUnit tests for business logic
│   ├── Unit.Tests.csproj
│   ├── Managers/                  # Manager unit tests
│   └── Domain/                    # Domain model unit tests
│
└── Integration.Tests/             # Playwright API integration tests
    ├── package.json               # Node dependencies
    ├── playwright.config.ts       # Playwright configuration
    ├── tests/                     # Test files (*.spec.ts)
    │   ├── feature-workflow.spec.ts
    │   └── api-validation.spec.ts
    ├── README.md                  # Test-specific documentation
    └── test-results/              # Generated test artifacts
```

## Getting Started

### 1. Install Playwright

```bash
cd test/Integration.Tests
npm install
npx playwright install  # Install browsers (first time only)
```

### 2. Configure playwright.config.ts

```typescript
import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  
  use: {
    baseURL: process.env.API_BASE_URL || 'http://localhost:7071/api',
    extraHTTPHeaders: {
      'Accept': 'application/json',
      'Content-Type': 'application/json'
    }
  },

  projects: [
    { name: 'api-tests', testMatch: '**/*.spec.ts' }
  ],

  reporter: [
    ['html'],
    ['list'],
    ['junit', { outputFile: 'test-results/junit.xml' }]
  ]
});
```

### 3. Write Your First Test

```typescript
// tests/example.spec.ts
import { test, expect } from '@playwright/test';
import { randomUUID } from 'crypto';

test.describe('Resource API Tests', () => {
  let resourceId: string;

  test.beforeEach(() => {
    // Generate unique test data each run
    resourceId = randomUUID();
  });

  test('Create and retrieve resource', async ({ request }) => {
    // Create resource
    const createResponse = await request.post('/resources', {
      data: {
        id: resourceId,
        name: 'Test Resource',
        value: 100
      }
    });
    
    expect(createResponse.status()).toBe(201);

    // Retrieve resource
    const getResponse = await request.get(`/resources/${resourceId}`);
    expect(getResponse.status()).toBe(200);
    
    const body = await getResponse.json();
    expect(body.id).toBe(resourceId);
    expect(body.name).toBe('Test Resource');
  });
});
```

## Test Patterns

### Pattern 1: Complete Workflow Testing

Test entire business workflows end-to-end:

```typescript
test('Complete order fulfillment workflow', async ({ request }) => {
  const orderId = randomUUID();
  
  // 1. Create order
  await request.post('/orders', {
    data: { id: orderId, items: [...] }
  });
  
  // 2. Confirm order
  await request.post(`/orders/${orderId}/confirm`);
  
  // 3. Process payment
  await request.post('/payments', {
    data: { orderId, amount: 100 }
  });
  
  // 4. Ship order
  const shipResponse = await request.post(`/orders/${orderId}/ship`);
  expect(shipResponse.status()).toBe(200);
  
  // 5. Verify final state
  const orderResponse = await request.get(`/orders/${orderId}`);
  const order = await orderResponse.json();
  expect(order.status).toBe('Shipped');
});
```

### Pattern 2: Validation Testing

Test business rule validation:

```typescript
test('Reject negative payment amounts', async ({ request }) => {
  const response = await request.post('/payments', {
    data: {
      accountId: randomUUID(),
      amount: -50.00
    }
  });
  
  expect(response.status()).toBe(400);
  const error = await response.json();
  expect(error.message).toContain('positive');
});
```

### Pattern 3: State Transition Testing

Test valid and invalid state transitions:

```typescript
test('Cannot activate already active account', async ({ request }) => {
  const accountId = randomUUID();
  
  // Create account (starts in Pending)
  await request.post('/accounts', { data: { id: accountId } });
  
  // Activate (Pending → Active) - should succeed
  const activate1 = await request.post(`/accounts/${accountId}/activate`);
  expect(activate1.status()).toBe(200);
  
  // Activate again (Active → Active) - should fail
  const activate2 = await request.post(`/accounts/${accountId}/activate`);
  expect(activate2.status()).toBe(400);
});
```

### Pattern 4: Data Verification

Verify calculations and data integrity:

```typescript
test('Balance calculation after multiple payments', async ({ request }) => {
  const accountId = randomUUID();
  
  // Create account with $500 premium
  await request.post('/accounts', {
    data: { id: accountId, premiumOwed: 500 }
  });
  
  await request.post(`/accounts/${accountId}/activate`);
  
  // Record 3 payments
  await request.post('/payments', { data: { accountId, amount: 150 }});
  await request.post('/payments', { data: { accountId, amount: 200 }});
  await request.post('/payments', { data: { accountId, amount: 100 }});
  
  // Verify totals
  const response = await request.get(`/accounts/${accountId}`);
  const account = await response.json();
  
  expect(account.totalPaid).toBe(450);
  expect(account.outstandingBalance).toBe(50);
});
```

## Best Practices

### 1. Generate Fresh Test Data

**Always** generate unique IDs for each test run:

```typescript
import { randomUUID } from 'crypto';

test.beforeEach(() => {
  // ✅ Good - unique per test
  accountId = randomUUID();
  customerId = `CUST-${Date.now()}`;
  
  // ❌ Bad - hardcoded values
  // accountId = 'test-account-123';
});
```

### 2. Use Descriptive Test Names

```typescript
// ✅ Good - describes what and why
test('Reject payment when amount exceeds outstanding balance', ...)

// ❌ Bad - vague
test('Payment validation', ...)
```

### 3. Arrange-Act-Assert Pattern

```typescript
test('Example test', async ({ request }) => {
  // Arrange - set up test data
  const accountId = randomUUID();
  await request.post('/accounts', { data: { id: accountId }});
  
  // Act - perform the action being tested
  const response = await request.post(`/accounts/${accountId}/activate`);
  
  // Assert - verify the outcome
  expect(response.status()).toBe(200);
});
```

### 4. Test Both Success and Failure Paths

```typescript
test.describe('Payment Recording', () => {
  test('Success - valid payment is recorded', async ({ request }) => {
    // ... test happy path
  });
  
  test('Failure - negative amount rejected', async ({ request }) => {
    // ... test validation
  });
  
  test('Failure - payment exceeds balance', async ({ request }) => {
    // ... test business rule
  });
});
```

### 5. Use Fixtures for Shared Setup

```typescript
// tests/fixtures.ts
export const test = baseTest.extend({
  activeAccount: async ({ request }, use) => {
    const accountId = randomUUID();
    await request.post('/accounts', { data: { id: accountId }});
    await request.post(`/accounts/${accountId}/activate`);
    await use({ id: accountId });
  }
});

// tests/payments.spec.ts
test('Record payment on active account', async ({ activeAccount, request }) => {
  // activeAccount is already set up
  const response = await request.post('/payments', {
    data: { accountId: activeAccount.id, amount: 100 }
  });
  expect(response.status()).toBe(200);
});
```

## Running Tests

### Local Development

```bash
# Run all tests
npm test

# Run specific test file
npm test -- billing-workflow.spec.ts

# Run tests matching pattern
npm test -- --grep "payment"

# Debug mode (step through test)
npm run test:debug

# UI mode (interactive test explorer)
npm run test:ui

# Show browser during test
npm run test:headed
```

### CI/CD Pipeline

```yaml
# Example: GitHub Actions
- name: Run Playwright Tests
  run: |
    cd test/Integration.Tests
    npm ci
    npx playwright install --with-deps
    npm test
  env:
    API_BASE_URL: ${{ secrets.STAGING_API_URL }}

- name: Upload Test Results
  if: always()
  uses: actions/upload-artifact@v3
  with:
    name: playwright-report
    path: test/Integration.Tests/playwright-report/
```

### Azure DevOps Pipeline

```yaml
- task: NodeTool@0
  inputs:
    versionSpec: '18.x'

- script: |
    cd test/Integration.Tests
    npm ci
    npx playwright install --with-deps
    npm test
  displayName: 'Run Integration Tests'
  env:
    API_BASE_URL: $(StagingApiUrl)

- task: PublishTestResults@2
  condition: always()
  inputs:
    testResultsFormat: 'JUnit'
    testResultsFiles: 'test/Integration.Tests/test-results/junit.xml'
```

## Environment Configuration

### Using Environment Variables

```typescript
// playwright.config.ts
export default defineConfig({
  use: {
    baseURL: process.env.API_BASE_URL || 'http://localhost:7071/api',
  },
});
```

Run with custom environment:
```bash
API_BASE_URL=https://staging.example.com/api npm test
```

### Multi-Environment Setup

```typescript
// playwright.config.ts
const environments = {
  local: 'http://localhost:7071/api',
  dev: 'https://dev-api.example.com/api',
  staging: 'https://staging-api.example.com/api'
};

export default defineConfig({
  use: {
    baseURL: environments[process.env.ENV || 'local'],
  },
});
```

```bash
ENV=staging npm test
```

## Debugging Tests

### 1. Use Debug Mode

```bash
npm run test:debug
```

Opens Playwright Inspector where you can:
- Step through test execution
- Inspect network requests/responses
- View element selectors
- Modify test code live

### 2. Add Console Logs

```typescript
test('Debug example', async ({ request }) => {
  const response = await request.get('/accounts');
  console.log('Status:', response.status());
  console.log('Body:', await response.json());
});
```

### 3. Use Trace Viewer

```typescript
// playwright.config.ts
export default defineConfig({
  use: {
    trace: 'on-first-retry', // Capture trace on failure
  },
});
```

View traces:
```bash
npx playwright show-trace test-results/.../trace.zip
```

## Common Assertions

```typescript
// Status codes
expect(response.status()).toBe(200);
expect(response.ok()).toBeTruthy();

// Response body
const body = await response.json();
expect(body).toHaveProperty('id');
expect(body.status).toBe('Active');
expect(body.amount).toBeGreaterThan(0);

// Arrays
expect(body.items).toHaveLength(3);
expect(body.tags).toContain('urgent');

// Objects
expect(body).toMatchObject({
  id: expect.any(String),
  createdAt: expect.stringMatching(/^\d{4}-\d{2}-\d{2}/)
});

// Headers
expect(response.headers()['content-type']).toContain('application/json');
```

## Troubleshooting

### Tests Fail with Connection Errors

1. Ensure API is running:
   ```bash
   dotnet run --project src/Api
   ```

2. Verify baseURL in `playwright.config.ts`

3. Check firewall/network settings

### Tests Fail Randomly

- Add retries in `playwright.config.ts`:
  ```typescript
  retries: process.env.CI ? 2 : 0
  ```

- Increase timeouts for slow operations:
  ```typescript
  test('Slow operation', async ({ request }) => {
    test.setTimeout(60000); // 60 seconds
    // ...
  });
  ```

### Tests Pass Locally but Fail in CI

- Check environment variables
- Verify database/dependencies are available
- Review CI logs for API errors
- Add more detailed logging

## Additional Resources

- [Playwright API Testing Docs](https://playwright.dev/docs/api-testing)
- [Playwright Configuration](https://playwright.dev/docs/test-configuration)
- [Playwright Assertions](https://playwright.dev/docs/test-assertions)
- [Playwright Fixtures](https://playwright.dev/docs/test-fixtures)
- [Playwright CI/CD](https://playwright.dev/docs/ci)

---

**Last Updated**: 2026-02-03
