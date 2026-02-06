import { test, expect } from '@playwright/test';

test.describe('Submit Underwriting', () => {
  // NOTE: Tests require quote in Draft status - create quote in beforeEach
  
  let quoteId: string;

  test.beforeEach(async ({ request }) => {
    quoteId = crypto.randomUUID();
    
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
  });

  test('should approve Class A underwriting', async ({ request }) => {
    const response = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
      data: {
        priorClaimsCount: 0,
        kwegiboAge: 10,
        creditTier: 'Excellent',
        zipCode: '60601'
      }
    });

    expect(response.status()).toBe(200);

    const result = await response.json();
    expect(result.status).toBe('Quoted');
    expect(result.underwritingClass).toBe('A');
    expect(result.premium).toBeGreaterThan(0);
  });

  test('should approve Class B underwriting', async ({ request }) => {
    const response = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
      data: {
        priorClaimsCount: 1,
        kwegiboAge: 25,
        creditTier: 'Good',
        zipCode: '60601'
      }
    });

    expect(response.status()).toBe(200);

    const result = await response.json();
    expect(result.status).toBe('Quoted');
    expect(result.underwritingClass).toBe('B');
  });

  test('should decline excessive claims', async ({ request }) => {
    const response = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
      data: {
        priorClaimsCount: 3,
        kwegiboAge: 15,
        creditTier: 'Good',
        zipCode: '60601'
      }
    });

    expect(response.status()).toBe(422);

    const error = await response.json();
    expect(error.error).toBe('UnderwritingDeclined');
    expect(error.message).toContain('claims');
  });

  test('should return 404 for non-existent quote', async ({ request }) => {
    const nonExistentId = crypto.randomUUID();

    const response = await request.post(`/api/quotes/${nonExistentId}/submit-underwriting`, {
      data: {
        priorClaimsCount: 0,
        kwegiboAge: 10,
        creditTier: 'Excellent',
        zipCode: '60601'
      }
    });

    expect(response.status()).toBe(404);
  });
});
