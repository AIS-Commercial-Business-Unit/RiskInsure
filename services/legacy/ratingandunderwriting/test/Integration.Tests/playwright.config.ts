import * as dotenv from 'dotenv';

dotenv.config();

import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1,
  timeout: 60000, // Default timeout of 60 seconds for all tests
  reporter: [
    ['html'],
    ['list'],
    ['junit', { outputFile: 'test-results/junit.xml' }]
  ],
  use: {
    baseURL: process.env.API_BASE_URL || 'http://localhost:7079',
    trace: 'on-first-retry',
    actionTimeout: 30000, // Timeout for individual actions (API calls)
    extraHTTPHeaders: {
      'Content-Type': 'application/json'
    }
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
