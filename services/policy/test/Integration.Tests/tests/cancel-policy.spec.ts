import { test, expect } from '@playwright/test';

test.describe('Cancel Policy', () => {
  // NOTE: Tests requiring existing policies deferred to enterprise integration tests
  // Only testing validation and error scenarios that the API layer controls
  
  test('should return 404 when policy not found', async ({ request }) => {
    const nonExistentPolicyId = crypto.randomUUID();

    const response = await request.post(`/api/policies/${nonExistentPolicyId}/cancel`, {
      data: {
        cancellationReason: 'Test',
        cancellationDate: new Date().toISOString()
      }
    });

    expect(response.status()).toBe(404);

    const error = await response.json();
    expect(error.error).toBe('PolicyNotFound');
  });

  test('should validate required fields', async ({ request }) => {
    const policyId = crypto.randomUUID();

    const response = await request.post(`/api/policies/${policyId}/cancel`, {
      data: {
        // Missing cancellationReason
        cancellationDate: new Date().toISOString()
      }
    });

    expect(response.status()).toBe(400);

    const error = await response.json();
    // ASP.NET Core returns ProblemDetails format for validation errors
    expect(error.status).toBe(400);
    expect(error.errors).toBeDefined();
    expect(error.errors.CancellationReason).toBeDefined();
    expect(Array.isArray(error.errors.CancellationReason)).toBe(true);
  });
});
