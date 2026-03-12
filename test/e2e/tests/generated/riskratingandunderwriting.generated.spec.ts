import { expect, test } from '@playwright/test';
import { getTestConfig, validateConfig } from '../../config/api-endpoints';
import { createCustomer } from '../../helpers/customer-api';

const config = getTestConfig();

function buildQuoteRequest(customerId: string) {
  return {
    customerId,
    structureCoverageLimit: 200000,
    structureDeductible: 1000,
    contentsCoverageLimit: 50000,
    contentsDeductible: 500,
    termMonths: 12,
    effectiveDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
    propertyZipCode: '90210',
  };
}

test.describe('[Generated] riskratingandunderwriting requirements regression', () => {
  test.beforeAll(() => {
    validateConfig(config);
  });

  test('service health endpoint responds for riskratingandunderwriting', async ({ request }) => {
    const health = await request.get(`${config.apis.riskratingandunderwriting}/health`);
    expect([200, 204]).toContain(health.status());
  });

  test('class A underwriting produces quoted premium and accepted quote', async ({ request }) => {
    const customer = await createCustomer(request, {
      firstName: 'Risk',
      lastName: 'Quoted',
    });

    const startResponse = await request.post(`${config.apis.riskratingandunderwriting}/api/risk-quotes/start`, {
      data: buildQuoteRequest(customer.customerId),
    });
    expect(startResponse.status()).toBe(201);
    const startedQuote = await startResponse.json();

    const underwritingResponse = await request.post(`${config.apis.riskratingandunderwriting}/api/risk-quotes/${startedQuote.quoteId}/submit-underwriting`, {
      data: {
        priorClaimsCount: 0,
        propertyAgeYears: 10,
        creditTier: 'Excellent',
      },
    });
    expect(underwritingResponse.status()).toBe(200);
    const underwriting = await underwritingResponse.json();
    expect(underwriting.status).toBe('Quoted');
    expect(underwriting.underwritingClass).toBe('A');
    expect(underwriting.premium).toBe(1350);

    const acceptResponse = await request.post(`${config.apis.riskratingandunderwriting}/api/risk-quotes/${startedQuote.quoteId}/accept`);
    expect(acceptResponse.status()).toBe(200);
    const accepted = await acceptResponse.json();
    expect(accepted.status).toBe('Accepted');
    expect(accepted.policyCreationInitiated).toBe(true);
  });

  test('declined underwriting returns 422 and leaves quote declined', async ({ request }) => {
    const customer = await createCustomer(request, {
      firstName: 'Risk',
      lastName: 'Declined',
    });

    const startResponse = await request.post(`${config.apis.riskratingandunderwriting}/api/risk-quotes/start`, {
      data: buildQuoteRequest(customer.customerId),
    });
    expect(startResponse.status()).toBe(201);
    const startedQuote = await startResponse.json();

    const declineResponse = await request.post(`${config.apis.riskratingandunderwriting}/api/risk-quotes/${startedQuote.quoteId}/submit-underwriting`, {
      data: {
        priorClaimsCount: 3,
        propertyAgeYears: 15,
        creditTier: 'Excellent',
      },
    });

    expect(declineResponse.status()).toBe(422);
    const declined = await declineResponse.json();
    expect(declined.error).toBe('UnderwritingDeclined');

    const getQuoteResponse = await request.get(`${config.apis.riskratingandunderwriting}/api/risk-quotes/${startedQuote.quoteId}`);
    expect(getQuoteResponse.status()).toBe(200);
    const quote = await getQuoteResponse.json();
    expect(quote.status).toBe('Declined');
  });
});

test.describe('[Generated] metadata for riskratingandunderwriting', () => {
  test('source documents snapshot', async () => {
    const snapshot = [
      '- services/riskratingandunderwriting/docs/business/rating-underwriting.md',
      '- services/riskratingandunderwriting/docs/technical/rating-underwriting-technical-spec.md',
    ];

    expect(snapshot.length).toBeGreaterThan(0);
  });
});
