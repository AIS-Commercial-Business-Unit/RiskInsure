# CustomerRelationshipsMgt Integration Tests

Playwright-based integration tests for the CustomerRelationshipsMgt API.

## Prerequisites

- Node.js 18+ installed
- CustomerRelationshipsMgt API running on `http://localhost:7077`
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

- ✅ Create relationship (valid and invalid scenarios)
- ✅ Get relationship by ID
- ✅ Update relationship information
- ✅ Email change request
- ✅ Close relationship account (GDPR anonymization)
