import { test, expect } from '@playwright/test';

test.describe('Get Customer Policies', () => {
  // NOTE: Tests requiring existing policies deferred to enterprise integration tests
  // Testing only the "no policies" scenario which domain fully controls
  
  test('should return empty array when customer has no policies', async ({ request }) => {
    const customerId = `CUST-${crypto.randomUUID()}`;

    const response = await request.get(`/api/policies/customers/${customerId}/policies`);

    expect(response.status()).toBe(200);

    const result = await response.json();
    expect(result.customerId).toBe(customerId);
    expect(result.policies).toEqual([]);
    expect(result.totalCount).toBe(0);
  });
});
