import { test, expect } from '@playwright/test';

test.describe('Accept Quote', () => {
  // NOTE: Tests require quote in Quoted status - create and underwrite in beforeEach
  
  let quoteId: string;

  test.beforeEach(async ({ request }) => {
    quoteId = crypto.randomUUID();
    
    // Start quote
    await request.post('/api/quotes/start', {
      data: {
        quoteId,
        customerId: crypto.randomUUID(),
        structureCoverageLimit: 200000,
        structureDeductible: 1000,
        contentsCoverageLimit: 50000,
        contentsDeductible: 500,
        termMonths: 12,
        effectiveDate: new Date(Date.now() + 86400000).toISOString()
      }
    });

    // Submit underwriting to get to Quoted status
    await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
      data: {
        priorClaimsCount: 0,
        kwegiboAge: 10,
        creditTier: 'Excellent',
        zipCode: '60601'
      }
    });
  });

  test('should accept quoted quote successfully', async ({ request }) => {
    const response = await request.post(`/api/quotes/${quoteId}/accept`);

    expect(response.status()).toBe(200);

    const result = await response.json();
    expect(result.status).toBe('Accepted');
    expect(result.acceptedUtc).toBeDefined();
    expect(result.premium).toBeGreaterThan(0);
    expect(result.message).toContain('Policy creation');
  });

  test('should return 404 for non-existent quote', async ({ request }) => {
    const nonExistentId = crypto.randomUUID();

    const response = await request.post(`/api/quotes/${nonExistentId}/accept`);

    expect(response.status()).toBe(404);
  });
});
