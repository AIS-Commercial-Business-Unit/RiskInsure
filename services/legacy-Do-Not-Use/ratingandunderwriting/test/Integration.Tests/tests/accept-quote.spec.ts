import { test, expect } from '@playwright/test';

test.describe('Accept Quote', () => {
  // NOTE: Tests require quote in Quoted status - create and underwrite in beforeEach
  
  let quoteId: string;

  test.beforeEach(async ({ request }) => {
    // Start quote
    const startResponse = await request.post('/api/quotes/start', {
      data: {
        customerId: crypto.randomUUID(),
        structureCoverageLimit: 200000,
        structureDeductible: 1000,
        contentsCoverageLimit: 50000,
        contentsDeductible: 500,
        termMonths: 12,
        effectiveDate: new Date(Date.now() + 86400000).toISOString(),
        propertyZipCode: '60601'
      }
    });
    
    const startResult = await startResponse.json();
    quoteId = startResult.quoteId;

    // Submit underwriting to get to Quoted status
    await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
      data: {
        priorClaimsCount: 0,
        propertyAgeYears: 10,
        creditTier: 'Excellent'
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
    const nonExistentId = 'QUOTE-' + Date.now();

    const response = await request.post(`/api/quotes/${nonExistentId}/accept`);

    expect(response.status()).toBe(404);
  });
});
