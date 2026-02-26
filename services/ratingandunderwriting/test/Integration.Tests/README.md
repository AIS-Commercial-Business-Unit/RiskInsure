# Rating & Underwriting Integration Tests

End-to-end API integration tests using Playwright.

## Prerequisites

- Node.js 18+ installed
- Rating & Underwriting API running on http://localhost:7079
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

### Start Quote (`tests/start-quote.spec.ts`)
- ✅ Create new quote successfully
- ✅ Validate coverage limits

### Submit Underwriting (`tests/submit-underwriting.spec.ts`)
- ✅ Approve Class A underwriting
- ✅ Approve Class B underwriting
- ✅ Decline excessive claims
- ✅ Return 404 for non-existent quote

### Accept Quote (`tests/accept-quote.spec.ts`)
- ✅ Accept quoted quote (triggers policy creation)
- ✅ Return 404 for non-existent quote

## Testing Philosophy

**Rating & Underwriting domain CAN create its own test data** via POST /api/quotes/start endpoint.

Therefore, these integration tests include full "happy path" scenarios:
- Creating quotes
- Submitting underwriting
- Accepting quotes (which publishes QuoteAccepted event to trigger policy creation in Policy domain)

Unlike Policy domain (which relies on QuoteAccepted events), Rating & Underwriting controls its entire quote lifecycle.

## Important Notes

### Validation Error Format

ASP.NET Core returns **ProblemDetails** format for model validation errors:

```typescript
expect(error.status).toBe(400);
expect(error.errors.FieldName).toBeDefined();
expect(Array.isArray(error.errors.FieldName)).toBe(true);
```

Business validation errors use custom format:
```typescript
expect(error.error).toBe('UnderwritingDeclined');
expect(error.message).toContain('claims');
```

## Configuration

See [playwright.config.ts](playwright.config.ts) for test configuration.
