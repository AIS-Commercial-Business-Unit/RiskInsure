import { test, expect, APIRequestContext } from '@playwright/test';

const tomorrow = () => new Date(Date.now() + 86400000).toISOString();

async function createDraftQuote(
  request: APIRequestContext,
  overrides: Record<string, unknown> = {}
): Promise<{ quoteId: string; customerId: string }> {
  const customerId = (overrides.customerId as string) ?? crypto.randomUUID();
  const response = await request.post('/api/quotes/start', {
    data: {
      customerId,
      structureCoverageLimit: 200000,
      structureDeductible: 1000,
      contentsCoverageLimit: 50000,
      contentsDeductible: 500,
      termMonths: 12,
      effectiveDate: tomorrow(),
      propertyZipCode: '60601',
      ...overrides,
    },
  });
  expect(response.status()).toBe(201);
  const body = await response.json();
  return { quoteId: body.quoteId, customerId };
}

async function createQuotedQuote(request: APIRequestContext): Promise<string> {
  const { quoteId } = await createDraftQuote(request);
  const uwResponse = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
    data: { priorClaimsCount: 0, propertyAgeYears: 10, creditTier: 'Excellent' },
  });
  expect(uwResponse.status()).toBe(200);
  return quoteId;
}

async function createAcceptedQuote(request: APIRequestContext): Promise<string> {
  const quoteId = await createQuotedQuote(request);
  const acceptResponse = await request.post(`/api/quotes/${quoteId}/accept`);
  expect(acceptResponse.status()).toBe(200);
  return quoteId;
}

test.describe('GET /api/quotes/{quoteId}', () => {
  test('returns full Draft quote with all fields', async ({ request }) => {
    const { quoteId, customerId } = await createDraftQuote(request);

    const response = await request.get(`/api/quotes/${quoteId}`);

    expect(response.status()).toBe(200);

    const body = await response.json();
    expect(body.quoteId).toBe(quoteId);
    expect(body.customerId).toBe(customerId);
    expect(body.status).toBe('Draft');
    expect(body.structureCoverageLimit).toBe(200000);
    expect(body.structureDeductible).toBe(1000);
    expect(body.contentsCoverageLimit).toBe(50000);
    expect(body.contentsDeductible).toBe(500);
    expect(body.termMonths).toBe(12);
    expect(body.effectiveDate).toBeDefined();
    expect(body.expirationUtc).toBeDefined();
    expect(body.createdUtc).toBeDefined();
    expect(body.premium).toBeNull();
    expect(body.underwritingClass).toBeNull();
  });

  test('returns Quoted quote with Class A premium after underwriting', async ({ request }) => {
    const { quoteId } = await createDraftQuote(request);

    const uwResponse = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
      data: { priorClaimsCount: 0, propertyAgeYears: 10, creditTier: 'Excellent' },
    });
    expect(uwResponse.status()).toBe(200);

    const response = await request.get(`/api/quotes/${quoteId}`);

    expect(response.status()).toBe(200);

    const body = await response.json();
    expect(body.quoteId).toBe(quoteId);
    expect(body.status).toBe('Quoted');
    expect(body.underwritingClass).toBe('A');
    expect(body.premium).toBeGreaterThan(0);
  });

  test('returns Quoted quote with Class B after moderate-risk underwriting', async ({ request }) => {
    const { quoteId } = await createDraftQuote(request);

    const uwResponse = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
      data: { priorClaimsCount: 1, propertyAgeYears: 25, creditTier: 'Good' },
    });
    expect(uwResponse.status()).toBe(200);

    const response = await request.get(`/api/quotes/${quoteId}`);

    expect(response.status()).toBe(200);

    const body = await response.json();
    expect(body.status).toBe('Quoted');
    expect(body.underwritingClass).toBe('B');
    expect(body.premium).toBeGreaterThan(0);
  });

  test('returns Accepted quote after full workflow', async ({ request }) => {
    const quoteId = await createAcceptedQuote(request);

    const response = await request.get(`/api/quotes/${quoteId}`);

    expect(response.status()).toBe(200);

    const body = await response.json();
    expect(body.quoteId).toBe(quoteId);
    expect(body.status).toBe('Accepted');
    expect(body.underwritingClass).toBe('A');
    expect(body.premium).toBeGreaterThan(0);
  });

  test('returns 404 with QuoteNotFound error for non-existent quoteId', async ({ request }) => {
    const response = await request.get('/api/quotes/QUOTE-000000000000');

    expect(response.status()).toBe(404);

    const error = await response.json();
    expect(error.error).toBe('QuoteNotFound');
  });
});
