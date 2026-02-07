import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  timeout: 30000,
  retries: 0,
  use: {
    baseURL: 'http://localhost:7085', // To be updated if port changes
    trace: 'on-first-retry',
  },
  reporter: [['list'], ['html', { outputFolder: 'playwright-report' }]],
});
