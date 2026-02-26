import { test, expect } from '@playwright/test';
import { randomUUID } from 'crypto';

test.describe('Get Policy', () => {
  // NOTE: Policy creation happens via QuoteAccepted events, not direct API calls
  // Tests requiring existing policies will be covered in enterprise integration tests
  
  test('should return 404 when policy not found', async ({ request }) => {
    const nonExistentPolicyId = randomUUID();

    const response = await request.get(`/api/policies/${nonExistentPolicyId}`);

    expect(response.status()).toBe(404);

    const error = await response.json();
    expect(error.error).toBe('PolicyNotFound');
  });
});
