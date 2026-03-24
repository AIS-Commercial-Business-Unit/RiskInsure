import { test, expect } from '@playwright/test';
import { randomUUID } from 'crypto';

test.describe('Get Customer Policies', () => {
  // NOTE: Tests requiring existing policies deferred to enterprise integration tests
  // Testing only the "no policies" scenario which domain fully controls
  
  test('should return empty array when customer has no policies', async ({ request }) => {
    const customerId = `CUST-${randomUUID()}`;

    const response = await request.get(`/api/customers/${customerId}/policies`);

    expect(response.status()).toBe(200);

    const result = await response.json();
    expect(result.customerId).toBe(customerId);
    expect(result.policies).toEqual([]);
  });
});
