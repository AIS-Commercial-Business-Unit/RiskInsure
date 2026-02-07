# NsbShipping Integration Tests

## Setup

1. Ensure the NsbShipping API is running on the assigned port (default: 7085).
2. Install dependencies:
   ```bash
   npm install
   npx playwright install chromium
   ```

## Running Tests

- Headless: `npm test`
- UI mode: `npm run test:ui`
- Headed: `npm run test:headed`
- Debug: `npm run test:debug`
- Report: `npm run test:report`

## Notes
- Only endpoints fully controlled by this domain are tested here.
- Tests requiring cross-domain data are deferred to enterprise integration tests.
