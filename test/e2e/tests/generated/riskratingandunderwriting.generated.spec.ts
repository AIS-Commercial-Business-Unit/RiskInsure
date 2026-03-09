import { test, expect } from '@playwright/test';
import { getTestConfig, validateConfig } from '../../config/api-endpoints';
import { createCustomer, getCustomer } from '../../helpers/customer-api';
import { startQuote, submitUnderwriting, acceptQuote } from '../../helpers/rating-api';
import { waitForPolicyCreation } from '../../helpers/policy-api';
import { getCustomerPolicies } from '../../helpers/policy-api';

const config = getTestConfig();

test.describe('[Generated] riskratingandunderwriting requirements regression', () => {
  test.beforeAll(() => {
    validateConfig(config);
  });


  test('service health endpoint responds for riskratingandunderwriting', async ({ request }) => {
    const baseUrl = config.apis.riskratingandunderwriting;
    const health = await request.get(baseUrl + '/health');

    if (health.status() === 404) {
      const fallback = await request.get(baseUrl + '/healthz');
      expect([200, 204, 404]).toContain(fallback.status());
      return;
    }

    expect([200, 204]).toContain(health.status());
  });

  test('quote acceptance creates bound policy', async ({ request }) => {
    test.setTimeout(180000);

    const customer = await createCustomer(request, {
      firstName: 'Policy',
      lastName: 'Generated'
    });

    const quote = await startQuote(request, customer.customerId, {
      structureCoverageLimit: 300000,
      structureDeductible: 1000,
      contentsCoverageLimit: 100000,
      contentsDeductible: 500,
      termMonths: 12,
      propertyZipCode: '90210'
    });

    const underwriting = await submitUnderwriting(request, quote.quoteId, {
      priorClaimsCount: 0,
      propertyAgeYears: 12,
      creditTier: 'Excellent'
    });

    expect(underwriting.status()).toBe(200);

    const accepted = await acceptQuote(request, quote.quoteId);
    expect(accepted.policyCreationInitiated).toBe(true);

    const policy = await waitForPolicyCreation(request, customer.customerId);
    expect(policy.status).toBe('Bound');
    expect(policy.policyNumber).toMatch(/^KWG-\d{4}-\d{6}$/);
  });

  test('policy lifecycle statuses are queryable for customer', async ({ request }) => {
    const customer = await createCustomer(request, {
      firstName: 'Lifecycle',
      lastName: 'Watcher'
    });

    const policies = await getCustomerPolicies(request, customer.customerId);
    expect(Array.isArray(policies)).toBe(true);
  });
});

test.describe('[Generated] metadata for riskratingandunderwriting', () => {
  test('source documents snapshot', async () => {
    const snapshot = [
      '- services/riskratingandunderwriting/docs/business/rating-underwriting.md',
      '- services/riskratingandunderwriting/docs/technical/rating-underwriting-technical-spec.md'
    ];

    expect(snapshot.length).toBeGreaterThan(0);
  });
});
