# Customer Integration Tests

Playwright-based integration tests for the Customer API.

## Prerequisites

- Node.js 18+ installed
- Customer API running on `http://localhost:7075`
- Cosmos DB Emulator running (for data persistence)

## Installation

```bash
npm install
npx playwright install chromium
```

## Running Tests

### Interactive UI Mode (Recommended)
```bash
npm run test:ui
```

### Headless Mode
```bash
npm test
```

### Headed Mode (Browser Visible)
```bash
npm run test:headed
```

### Debug Mode
```bash
npm run test:debug
```

### View Test Report
```bash
npm run test:report
```

## Test Coverage

- ✅ Create customer (valid and invalid scenarios)
- ✅ Get customer by ID
- ✅ Update customer information
- ✅ Email change request
- ✅ Delete customer (GDPR compliance)
- ✅ Validation: email format, age, zip code
- ✅ Duplicate email rejection
- ✅ 404 handling for non-existent customers

## Test Data

All tests use unique GUIDs and random email addresses to avoid conflicts. Tests are safe to run multiple times.

## Troubleshooting

**Connection refused**: Ensure the Customer API is running on port 7075
```bash
cd ../../src/Api
dotnet run
```

**Tests fail**: Check that Cosmos DB Emulator is running and connection string is configured in `appsettings.Development.json`
