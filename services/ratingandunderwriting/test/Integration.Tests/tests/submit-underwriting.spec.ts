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

test.describe('POST /api/quotes/{quoteId}/submit-underwriting', () => {
  let quoteId: string;

  test.beforeEach(async ({ request }) => {
    quoteId = await createDraftQuote(request);
  });

  test('returns Quoted status with Class A underwriting for low-risk profile', async ({ request }) => {
    const response = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
      data: {
        priorClaimsCount: 0,
        propertyAgeYears: 10,
        creditTier: 'Excellent',
      },
    });

    expect(response.status()).toBe(200);

    const body = await response.json();
    expect(body.quoteId).toBe(quoteId);
    expect(body.status).toBe('Quoted');
    expect(body.underwritingClass).toBe('A');
    expect(body.premium).toBeGreaterThan(0);
    expect(body.expirationUtc).toBeDefined();
  });

  test('returns Quoted status with Class B underwriting for moderate-risk profile', async ({ request }) => {
    const response = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
      data: {
        priorClaimsCount: 1,
        propertyAgeYears: 25,
        creditTier: 'Good',
      },
    });

    expect(response.status()).toBe(200);

    const body = await response.json();
    expect(body.quoteId).toBe(quoteId);
    expect(body.status).toBe('Quoted');
    expect(body.underwritingClass).toBe('B');
    expect(body.premium).toBeGreaterThan(0);
  });

  test('Class B premium is higher than Class A premium for same coverage', async ({ request }) => {
    const classAQuoteId = await createDraftQuote(request);
    const classBQuoteId = await createDraftQuote(request);

    const [classAResponse, classBResponse] = await Promise.all([
      request.post(`/api/quotes/${classAQuoteId}/submit-underwriting`, {
        data: { priorClaimsCount: 0, propertyAgeYears: 10, creditTier: 'Excellent' },
      }),
      request.post(`/api/quotes/${classBQuoteId}/submit-underwriting`, {
        data: { priorClaimsCount: 1, propertyAgeYears: 25, creditTier: 'Good' },
      }),
    ]);

    expect(classAResponse.status()).toBe(200);
    expect(classBResponse.status()).toBe(200);

    const [classA, classB] = await Promise.all([classAResponse.json(), classBResponse.json()]);
    expect(classB.premium).toBeGreaterThan(classA.premium);
  });

  test('returns 422 UnderwritingDeclined when prior claims count is excessive', async ({ request }) => {
    const response = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
      data: {
        priorClaimsCount: 3,
        propertyAgeYears: 15,
        creditTier: 'Good',
      },
    });

    expect(response.status()).toBe(422);

    const error = await response.json();
    expect(error.error).toBe('UnderwritingDeclined');
    expect(error.message).toBeDefined();
  });

  test('returns 409 when submitting underwriting on an already-quoted quote', async ({ request }) => {
    // Bring quote to Quoted status
    const firstResponse = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
      data: { priorClaimsCount: 0, propertyAgeYears: 10, creditTier: 'Excellent' },
    });
    expect(firstResponse.status()).toBe(200);

    // Second submission on the same quote should conflict
    const secondResponse = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
      data: { priorClaimsCount: 0, propertyAgeYears: 10, creditTier: 'Excellent' },
    });

    expect(secondResponse.status()).toBe(409);

    const error = await secondResponse.json();
    expect(error.error).toBe('InvalidQuoteStatus');
  });

  test('returns 404 for a non-existent quoteId', async ({ request }) => {
    const response = await request.post('/api/quotes/QUOTE-000000000000/submit-underwriting', {
      data: { priorClaimsCount: 0, propertyAgeYears: 10, creditTier: 'Excellent' },
    });

    expect(response.status()).toBe(404);

    const error = await response.json();
    expect(error.error).toBe('QuoteNotFound');
  });

  test('returns 400 when creditTier is missing', async ({ request }) => {
    const response = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
      data: { priorClaimsCount: 0, propertyAgeYears: 10 },
    });

    expect(response.status()).toBe(400);
  });
});
