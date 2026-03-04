import { test, expect } from '@playwright/test';
import { randomUUID } from 'crypto';

test.describe('Policy Lifecycle Overlap', () => {
  test('should return empty lifecycle list for policy with no term states', async ({ request }) => {
    const policyId = randomUUID();

    const response = await request.get(`/api/policies/${policyId}/lifecycle/terms`);

    expect(response.status()).toBe(200);

    const body = await response.json();
    expect(Array.isArray(body)).toBeTruthy();
    expect(body.length).toBe(0);
  });
});
