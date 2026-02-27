import { test, expect, APIRequestContext } from '@playwright/test';

const tomorrow = () => new Date(Date.now() + 86400000).toISOString();

async function createDraftQuote(request: APIRequestContext): Promise<string> {
  const response = await request.post('/api/quotes/start', {
    data: {
      customerId: crypto.randomUUID(),
      structureCoverageLimit: 200000,
      structureDeductible: 1000,
      contentsCoverageLimit: 50000,
      contentsDeductible: 500,
      termMonths: 12,
      effectiveDate: tomorrow(),
      propertyZipCode: '60601',
    },
  });
  expect(response.status()).toBe(201);
  const body = await response.json();
  return body.quoteId;
}

async function createQuotedQuote(request: APIRequestContext): Promise<string> {
  const quoteId = await createDraftQuote(request);
  const response = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
    data: { priorClaimsCount: 0, propertyAgeYears: 10, creditTier: 'Excellent' },
  });
  expect(response.status()).toBe(200);
  return quoteId;
}

test.describe('POST /api/quotes/{quoteId}/accept', () => {
  test.describe('accepting a Quoted quote', () => {
    let quoteId: string;

    test.beforeEach(async ({ request }) => {
      quoteId = await createQuotedQuote(request);
    });

    test('returns Accepted status with premium and policy creation confirmation', async ({ request }) => {
      const response = await request.post(`/api/quotes/${quoteId}/accept`);

      expect(response.status()).toBe(200);

      const body = await response.json();
      expect(body.quoteId).toBe(quoteId);
      expect(body.status).toBe('Accepted');
      expect(body.premium).toBeGreaterThan(0);
      expect(body.acceptedUtc).toBeDefined();
      expect(body.message).toContain('Policy creation');
      expect(body.policyCreationInitiated).toBe(true);
    });

    test('returns 409 when accepting an already-accepted quote', async ({ request }) => {
      const firstAccept = await request.post(`/api/quotes/${quoteId}/accept`);
      expect(firstAccept.status()).toBe(200);

      const secondAccept = await request.post(`/api/quotes/${quoteId}/accept`);
      expect(secondAccept.status()).toBe(409);

      const error = await secondAccept.json();
      expect(error.error).toBe('InvalidQuoteStatus');
    });
  });

  test.describe('error cases', () => {
    test('returns 404 for a non-existent quoteId', async ({ request }) => {
      const response = await request.post('/api/quotes/QUOTE-000000000000/accept');

      expect(response.status()).toBe(404);

      const error = await response.json();
      expect(error.error).toBe('QuoteNotFound');
    });

    test('returns 409 when accepting a Draft quote that has not been underwritten', async ({ request }) => {
      const quoteId = await createDraftQuote(request);

      const response = await request.post(`/api/quotes/${quoteId}/accept`);

      expect(response.status()).toBe(409);

      const error = await response.json();
      expect(error.error).toBe('InvalidQuoteStatus');
    });
  });
});
