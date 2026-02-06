import { test, expect } from '@playwright/test';

test.describe('Start Quote', () => {
  // NOTE: Domain can create quotes directly - test full operation
  
  test('should start new quote successfully', async ({ request }) => {
    const quoteId = crypto.randomUUID();
    const customerId = crypto.randomUUID();

    const response = await request.post('/api/quotes/start', {
      data: {
        quoteId,
        customerId,
        structureCoverageLimit: 200000,
        structureDeductible: 1000,
        contentsCoverageLimit: 50000,
        contentsDeductible: 500,
        termMonths: 12,
        effectiveDate: new Date(Date.now() + 86400000).toISOString()
      }
    });

    expect(response.status()).toBe(201);

    const result = await response.json();
    expect(result.quoteId).toBe(quoteId);
    expect(result.status).toBe('Draft');
    expect(result.expirationUtc).toBeDefined();
  });

  test('should validate coverage limits', async ({ request }) => {
    const quoteId = crypto.randomUUID();

    const response = await request.post('/api/quotes/start', {
      data: {
        quoteId,
        customerId: crypto.randomUUID(),
        structureCoverageLimit: 30000, // Below minimum
        structureDeductible: 1000,
        contentsCoverageLimit: 50000,
        contentsDeductible: 500,
        termMonths: 12,
        effectiveDate: new Date(Date.now() + 86400000).toISOString()
      }
    });

    expect(response.status()).toBe(400);

    const error = await response.json();
    expect(error.errors.StructureCoverageLimit).toBeDefined();
  });
});
