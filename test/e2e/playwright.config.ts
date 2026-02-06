import { defineConfig, devices } from '@playwright/test';

/**
 * E2E Test Configuration for RiskInsure
 * 
 * Environment Variables (override defaults):
 * - CUSTOMER_API_URL: Customer domain API base URL
 * - RATING_API_URL: Rating & Underwriting domain API base URL
 * - POLICY_API_URL: Policy domain API base URL
 * - BILLING_API_URL: Billing domain API base URL
 * - FUNDS_TRANSFER_API_URL: Funds Transfer Management domain API base URL
 * - EVENTUAL_CONSISTENCY_TIMEOUT: Max wait time for async events (ms)
 * - API_REQUEST_TIMEOUT: Max wait time for API requests (ms)
 */
export default defineConfig({
  testDir: './tests',
  fullyParallel: false, // Run tests sequentially to avoid eventual consistency conflicts
  forbidOnly: !!process.env.CI, // Fail CI if test.only() is left in
  retries: process.env.CI ? 2 : 0, // Retry failed tests in CI for transient issues
  workers: 1, // Single worker for E2E tests (cross-domain state dependencies)
  reporter: [
    ['html'],
    ['list'],
    ['json', { outputFile: 'test-results.json' }]
  ],
  
  use: {
    // API request defaults
    extraHTTPHeaders: {
      'Content-Type': 'application/json',
      'Accept': 'application/json'
    },
    trace: process.env.CI ? 'on-first-retry' : 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },

  projects: [
    {
      name: 'e2e-api-tests',
      use: { 
        ...devices['Desktop Chrome'],
        // API base URLs with defaults for local development
        baseURL: undefined, // Not used - we use per-domain URLs
      },
    },
  ],

  // Global timeout for entire test run
  globalTimeout: 15 * 60 * 1000, // 15 minutes
  
  // Per-test timeout
  timeout: 60 * 1000, // 60 seconds per test
});
