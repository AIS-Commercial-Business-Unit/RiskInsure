import { test, expect } from '@playwright/test';
import { randomUUID } from 'crypto';

test.describe('Policy Lifecycle Core', () => {
  test('should return 404 for missing lifecycle term', async ({ request }) => {
    const policyId = randomUUID();
    const termId = randomUUID();

    const response = await request.get(`/api/policies/${policyId}/lifecycle/terms/${termId}`);

    expect(response.status()).toBe(404);
  });

  test('should reject invalid termTicks on lifecycle start', async ({ request }) => {
    const policyId = randomUUID();

    const response = await request.post(`/api/policies/${policyId}/lifecycle/start`, {
      data: {
        policyTermId: randomUUID(),
        effectiveDateUtc: new Date().toISOString(),
        expirationDateUtc: new Date(Date.now() + 86400000).toISOString(),
        termTicks: 0,
        renewalOpenPercent: 66,
        renewalReminderPercent: 83,
        termEndPercent: 100,
        cancellationThresholdPercentage: -20,
        graceWindowPercent: 10
      }
    });

    expect(response.status()).toBe(400);
  });
});
