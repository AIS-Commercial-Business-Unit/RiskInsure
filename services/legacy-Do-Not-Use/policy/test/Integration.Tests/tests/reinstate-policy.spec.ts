import { test, expect } from '@playwright/test';
import { randomUUID } from 'crypto';

test.describe('Reinstate Policy', () => {
  // NOTE: Tests requiring existing policies deferred to enterprise integration tests
  // Only testing error scenarios that the API layer controls
  
  test('should return 404 when policy not found', async ({ request }) => {
    const nonExistentPolicyId = randomUUID();

    const response = await request.post(`/api/policies/${nonExistentPolicyId}/reinstate`);

    expect(response.status()).toBe(404);

    const error = await response.json();
    expect(error.error).toBe('PolicyNotFound');
  });
});
