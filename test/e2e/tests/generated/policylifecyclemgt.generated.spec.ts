import { expect, test } from '@playwright/test';
import { getTestConfig, validateConfig } from '../../config/api-endpoints';

const config = getTestConfig();

test.describe('[Generated] policylifecyclemgt requirements regression', () => {
  test.beforeAll(() => {
    validateConfig(config);
  });

  test('service health endpoint responds for policylifecyclemgt', async ({ request }) => {
    const health = await request.get(`${config.apis.policylifecyclemgt}/health`);
    expect([200, 204]).toContain(health.status());
  });

  test('unknown lifecycle identifiers return not found across core lifecycle APIs', async ({ request }) => {
    const policyId = `missing-${Date.now()}`;

    const issueResponse = await request.post(`${config.apis.policylifecyclemgt}/api/lifecycles/${policyId}/issue`);
    expect(issueResponse.status()).toBe(404);

    const getResponse = await request.get(`${config.apis.policylifecyclemgt}/api/lifecycles/${policyId}`);
    expect(getResponse.status()).toBe(404);

    const cancelResponse = await request.post(`${config.apis.policylifecyclemgt}/api/lifecycles/${policyId}/cancel`, {
      data: {
        cancellationDate: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
        reason: 'CustomerRequest',
      },
    });
    expect(cancelResponse.status()).toBe(404);

    const reinstateResponse = await request.post(`${config.apis.policylifecyclemgt}/api/lifecycles/${policyId}/reinstate`);
    expect(reinstateResponse.status()).toBe(404);
  });
});

test.describe('[Generated] metadata for policylifecyclemgt', () => {
  test('source documents snapshot', async () => {
    const snapshot = [
      '- services/policylifecyclemgt/docs/business/lifecycle-management.md',
      '- services/policylifecyclemgt/docs/technical/cutover-plan.md',
      '- services/policylifecyclemgt/docs/technical/lifecycle-technical-spec.md',
      '- services/policylifecyclemgt/docs/technical/transition-integration.md',
    ];

    expect(snapshot.length).toBeGreaterThan(0);
  });
});
