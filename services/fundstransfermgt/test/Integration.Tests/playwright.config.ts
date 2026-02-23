import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright configuration for Fund Transfer Management API integration tests.
 * See https://playwright.dev/docs/test-configuration.
 */
export default defineConfig({
  testDir: './tests',
  
  /* Maximum time one test can run for */
  timeout: 60 * 1000, // 60 seconds (increased for Cosmos DB Emulator slowness)
  
  /* Run tests in files in parallel */
  fullyParallel: true,
  
  /* Fail the build on CI if you accidentally left test.only in the source code. */
  forbidOnly: !!process.env.CI,
  
  /* Retry on CI only */
  retries: process.env.CI ? 2 : 0,
  
  /* Opt out of parallel tests on CI. */
  workers: process.env.CI ? 1 : undefined,
  
  /* Reporter to use. See https://playwright.dev/docs/test-reporters */
  reporter: [
    ['html'],
    ['list'],
    ['junit', { outputFile: 'test-results/junit.xml' }]
  ],
  
  /* Shared settings for all the projects below. See https://playwright.dev/docs/api/class-testoptions. */
  use: {
    /* Base URL to use in actions like `await page.goto('/')`. */
    baseURL: process.env.API_BASE_URL || 'http://localhost:7073',
    
    /* Collect trace when retrying the failed test. See https://playwright.dev/docs/trace-viewer */
    trace: 'on-first-retry',
    
    /* Extra HTTP headers */
    extraHTTPHeaders: {
      'Accept': 'application/json',
      'Content-Type': 'application/json'
    }
  },

  /* Configure projects for different scenarios */
  projects: [
    {
      name: 'api-tests',
      testMatch: '**/*.spec.ts'
    },
  ],

  /* Run your local dev server before starting the tests */
  // webServer: {
  //   command: 'dotnet run --project ../../src/Api',
  //   url: 'http://localhost:7073',
  //   reuseExistingServer: !process.env.CI,
  // },
});
