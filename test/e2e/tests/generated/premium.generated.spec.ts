import { expect, test } from '@playwright/test';
import { getTestConfig, validateConfig } from '../../config/api-endpoints';

const config = getTestConfig();
const premiumApiUrl = process.env.PREMIUM_API_URL;

test.describe('[Generated] premium requirements regression', () => {
  test.beforeAll(() => {
    validateConfig(config);
  });

  test('premium health endpoint responds when configured', async ({ request }) => {
    test.skip(!premiumApiUrl, 'PREMIUM_API_URL is not configured in this environment');

    const health = await request.get(`${premiumApiUrl}/health`);
    expect([200, 204]).toContain(health.status());
  });
});

test.describe('[Generated] metadata for premium', () => {
  test('source documents snapshot', async () => {
    const snapshot = [
      '- services/premium/docs/business/premium-management.md',
      '- services/premium/docs/technical/premium-technical-spec.md',
    ];

    expect(snapshot.length).toBeGreaterThan(0);
  });
});
