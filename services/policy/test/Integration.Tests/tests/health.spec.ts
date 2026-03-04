import { test, expect } from '@playwright/test';

test.describe('Health Check', () => {
  test('should return 200 OK from /health endpoint', async ({ request }) => {
    const response = await request.get('/health');

    expect(response.status()).toBe(200);
  });
});
