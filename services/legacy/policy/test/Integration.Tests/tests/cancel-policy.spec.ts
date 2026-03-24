import { test, expect } from '@playwright/test';
import { randomUUID } from 'crypto';

test.describe('Cancel Policy', () => {
  // NOTE: Tests requiring existing policies deferred to enterprise integration tests
  // Only testing validation and error scenarios that the API layer controls
  
  test('should return 404 when policy not found', async ({ request }) => {
    const nonExistentPolicyId = randomUUID();

    const response = await request.post(`/api/policies/${nonExistentPolicyId}/cancel`, {
      data: {
        reason: 'Test',
        cancellationDate: new Date().toISOString()
      }
    });

    expect(response.status()).toBe(404);

    const error = await response.json();
    expect(error.error).toBe('PolicyNotFound');
    expect(error.message).toContain(nonExistentPolicyId);
  });

  test('should validate required fields', async ({ request }) => {
    const policyId = randomUUID();

    const response = await request.post(`/api/policies/${policyId}/cancel`, {
      data: {
        // reason: "Unhappy",
        cancellationDate: new Date().toISOString()
      }
    });

    expect(response.status()).toBe(400);

    const error = await response.json();
    
    // Validation error for missing required property comes back as:
    // { status: 400, errors: { "$": ["...missing required properties including: 'reason'..."] } }
    expect(error.status).toBe(400);
    expect(error.errors).toBeDefined();
    
    // Check that the error mentions the missing 'reason' field
    const errorMessages = Object.values(error.errors).flat() as string[];
    const hasReasonError = errorMessages.some((msg: string) => 
      msg.toLowerCase().includes('reason')
    );
    expect(hasReasonError).toBe(true);
  });
});
