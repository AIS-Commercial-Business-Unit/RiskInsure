import { test, expect, APIRequestContext } from '@playwright/test';

const tomorrow = (daysAhead = 1) => new Date(Date.now() + 86400000 * daysAhead).toISOString();

async function createDraftQuote(
  request: APIRequestContext,
  customerId: string,
  overrides: Record<string, unknown> = {}
): Promise<string> {
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
  return body.quoteId;
}

async function submitUnderwriting(
  request: APIRequestContext,
  quoteId: string
): Promise<void> {
  const response = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
    data: { priorClaimsCount: 0, propertyAgeYears: 10, creditTier: 'Excellent' },
  });
  expect(response.status()).toBe(200);
}

async function acceptQuote(request: APIRequestContext, quoteId: string): Promise<void> {
  const response = await request.post(`/api/quotes/${quoteId}/accept`);
  expect(response.status()).toBe(200);
}

test.describe('GET /api/customers/{customerId}/quotes', () => {
  test('returns empty list for a customer with no quotes', async ({ request }) => {
    const customerId = crypto.randomUUID();

    const response = await request.get(`/api/customers/${customerId}/quotes`);

    expect(response.status()).toBe(200);

    const body = await response.json();
    expect(body.customerId).toBe(customerId);
    expect(body.quotes).toEqual([]);
  });

  test('returns all quotes for a customer', async ({ request }) => {
    const customerId = crypto.randomUUID();

    const [id1, id2] = await Promise.all([
      createDraftQuote(request, customerId, { effectiveDate: tomorrow(1) }),
      createDraftQuote(request, customerId, { effectiveDate: tomorrow(2) }),
    ]);

    const response = await request.get(`/api/customers/${customerId}/quotes`);

    expect(response.status()).toBe(200);

    const body = await response.json();
    expect(body.customerId).toBe(customerId);
    expect(body.quotes).toHaveLength(2);

    const returnedIds = body.quotes.map((q: { quoteId: string }) => q.quoteId);
    expect(returnedIds).toContain(id1);
    expect(returnedIds).toContain(id2);
  });

  test('returns quote summary with correct fields for a Draft quote', async ({ request }) => {
    const customerId = crypto.randomUUID();
    const quoteId = await createDraftQuote(request, customerId);

    const response = await request.get(`/api/customers/${customerId}/quotes`);

    expect(response.status()).toBe(200);

    const body = await response.json();
    expect(body.quotes).toHaveLength(1);

    const summary = body.quotes[0];
    expect(summary.quoteId).toBe(quoteId);
    expect(summary.status).toBe('Draft');
    expect(summary.premium).toBeNull();
    expect(summary.expirationUtc).toBeDefined();
    expect(summary.createdUtc).toBeDefined();
  });

  test('returns premium in summary after underwriting', async ({ request }) => {
    const customerId = crypto.randomUUID();
    const quoteId = await createDraftQuote(request, customerId);
    await submitUnderwriting(request, quoteId);

    const response = await request.get(`/api/customers/${customerId}/quotes`);

    expect(response.status()).toBe(200);

    const body = await response.json();
    expect(body.quotes).toHaveLength(1);

    const summary = body.quotes[0];
    expect(summary.quoteId).toBe(quoteId);
    expect(summary.status).toBe('Quoted');
    expect(summary.premium).toBeGreaterThan(0);
  });

  test('returns quotes across all statuses (Draft, Quoted, Accepted)', async ({ request }) => {
    const customerId = crypto.randomUUID();

    const draftId = await createDraftQuote(request, customerId, { effectiveDate: tomorrow(1) });

    const quotedId = await createDraftQuote(request, customerId, { effectiveDate: tomorrow(2) });
    await submitUnderwriting(request, quotedId);

    const acceptedId = await createDraftQuote(request, customerId, { effectiveDate: tomorrow(3) });
    await submitUnderwriting(request, acceptedId);
    await acceptQuote(request, acceptedId);

    const response = await request.get(`/api/customers/${customerId}/quotes`);

    expect(response.status()).toBe(200);

    const body = await response.json();
    expect(body.quotes).toHaveLength(3);

    const statuses = body.quotes.map((q: { status: string }) => q.status).sort();
    expect(statuses).toEqual(['Accepted', 'Draft', 'Quoted']);

    const ids = body.quotes.map((q: { quoteId: string }) => q.quoteId);
    expect(ids).toContain(draftId);
    expect(ids).toContain(quotedId);
    expect(ids).toContain(acceptedId);
  });

  test('does not return quotes belonging to other customers', async ({ request }) => {
    const customerId = crypto.randomUUID();
    const otherCustomerId = crypto.randomUUID();

    const quoteId = await createDraftQuote(request, customerId);
    await createDraftQuote(request, otherCustomerId);

    const response = await request.get(`/api/customers/${customerId}/quotes`);

    expect(response.status()).toBe(200);

    const body = await response.json();
    expect(body.customerId).toBe(customerId);
    expect(body.quotes).toHaveLength(1);
    expect(body.quotes[0].quoteId).toBe(quoteId);
  });
});
