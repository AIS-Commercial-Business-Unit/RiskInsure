import { test, expect } from '@playwright/test';
import { randomUUID } from 'crypto';

test.describe('Issue Policy', () => {
  // NOTE: Policy creation happens via QuoteAccepted events from Rating & Underwriting domain
  // Tests requiring existing policies will be covered in enterprise integration tests
  
  test('should return 404 when policy not found', async ({ request }) => {
    const nonExistentPolicyId = randomUUID();

    const response = await request.post(`/api/policies/${nonExistentPolicyId}/issue`);

    expect(response.status()).toBe(404);

    const error = await response.json();
    expect(error.error).toBe('PolicyNotFound');
    expect(error.message).toContain(nonExistentPolicyId);
  });
});
