import { test, expect, APIRequestContext } from '@playwright/test';

const tomorrow = () => new Date(Date.now() + 86400000).toISOString();

async function startQuote(request: APIRequestContext, overrides: Record<string, unknown> = {}) {
  return request.post('/api/quotes/start', {
    data: {
      customerId: crypto.randomUUID(),
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
}

test.describe('POST /api/quotes/start', () => {
  test('creates a draft quote and returns all response fields', async ({ request }) => {
    const customerId = crypto.randomUUID();

    const response = await startQuote(request, { customerId });

    expect(response.status()).toBe(201);

    const body = await response.json();
    expect(body.quoteId).toMatch(/^QUOTE-\d+$/);
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

  test('creates a quote without optional contents coverage', async ({ request }) => {
    const response = await request.post('/api/quotes/start', {
      data: {
        customerId: crypto.randomUUID(),
        structureCoverageLimit: 150000,
        structureDeductible: 2000,
        termMonths: 6,
        effectiveDate: tomorrow(),
        propertyZipCode: '10001',
      },
    });

    expect(response.status()).toBe(201);

    const body = await response.json();
    expect(body.quoteId).toMatch(/^QUOTE-/);
    expect(body.status).toBe('Draft');
  });

  test('each request produces a unique quoteId', async ({ request }) => {
    const customerId = crypto.randomUUID();

    const [r1, r2] = await Promise.all([
      startQuote(request, { customerId }),
      startQuote(request, { customerId }),
    ]);

    expect(r1.status()).toBe(201);
    expect(r2.status()).toBe(201);

    const [b1, b2] = await Promise.all([r1.json(), r2.json()]);
    expect(b1.quoteId).not.toBe(b2.quoteId);
  });

  test('returns 400 when structureCoverageLimit is below minimum (50000)', async ({ request }) => {
    const response = await startQuote(request, { structureCoverageLimit: 49999 });

    expect(response.status()).toBe(400);

    const error = await response.json();
    expect(error.errors?.StructureCoverageLimit).toBeDefined();
  });

  test('returns 400 when structureCoverageLimit exceeds maximum (500000)', async ({ request }) => {
    const response = await startQuote(request, { structureCoverageLimit: 500001 });

    expect(response.status()).toBe(400);

    const error = await response.json();
    expect(error.errors?.StructureCoverageLimit).toBeDefined();
  });

  test('returns 400 when contentsCoverageLimit is below minimum (10000)', async ({ request }) => {
    const response = await startQuote(request, { contentsCoverageLimit: 9999 });

    expect(response.status()).toBe(400);

    const error = await response.json();
    expect(error.errors?.ContentsCoverageLimit).toBeDefined();
  });

  test('returns 400 when contentsCoverageLimit exceeds maximum (150000)', async ({ request }) => {
    const response = await startQuote(request, { contentsCoverageLimit: 150001 });

    expect(response.status()).toBe(400);

    const error = await response.json();
    expect(error.errors?.ContentsCoverageLimit).toBeDefined();
  });

  test('returns 400 when customerId is missing', async ({ request }) => {
    const response = await request.post('/api/quotes/start', {
      data: {
        structureCoverageLimit: 200000,
        structureDeductible: 1000,
        termMonths: 12,
        effectiveDate: tomorrow(),
        propertyZipCode: '60601',
      },
    });

    expect(response.status()).toBe(400);
  });

  test('returns 400 when effectiveDate is missing', async ({ request }) => {
    const response = await request.post('/api/quotes/start', {
      data: {
        customerId: crypto.randomUUID(),
        structureCoverageLimit: 200000,
        structureDeductible: 1000,
        termMonths: 12,
        propertyZipCode: '60601',
      },
    });

    expect(response.status()).toBe(400);
  });

  test('returns 400 when propertyZipCode is missing', async ({ request }) => {
    const response = await request.post('/api/quotes/start', {
      data: {
        customerId: crypto.randomUUID(),
        structureCoverageLimit: 200000,
        structureDeductible: 1000,
        termMonths: 12,
        effectiveDate: tomorrow(),
      },
    });

    expect(response.status()).toBe(400);
  });
});
