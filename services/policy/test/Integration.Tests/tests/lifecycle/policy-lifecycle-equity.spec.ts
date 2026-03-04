import { test, expect } from '@playwright/test';
import { randomUUID } from 'crypto';

test.describe('Policy Lifecycle Equity', () => {
  test('should return 404 for missing lifecycle term on equity update', async ({ request }) => {
    const policyId = randomUUID();
    const termId = randomUUID();

    const response = await request.post(`/api/policies/${policyId}/lifecycle/equity-update`, {
      data: {
        policyTermId: termId,
        equityPercentage: -25,
        cancellationThresholdPercentage: -20,
        occurredUtc: new Date().toISOString()
      }
    });

    expect(response.status()).toBe(404);
  });
});
