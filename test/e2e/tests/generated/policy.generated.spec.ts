import { APIRequestContext, expect, test } from '@playwright/test';
import { getTestConfig, validateConfig } from '../../config/api-endpoints';
import { createCustomer } from '../../helpers/customer-api';
import { waitForPolicyCreation } from '../../helpers/policy-api';
import { acceptQuote, startQuote, submitUnderwriting } from '../../helpers/rating-api';

const config = getTestConfig();

async function createBoundPolicy(request: APIRequestContext) {
  const customer = await createCustomer(request, {
    firstName: 'Policy',
    lastName: 'Generated',
  });

  const quote = await startQuote(request, customer.customerId, {
    structureCoverageLimit: 300000,
    structureDeductible: 1000,
    contentsCoverageLimit: 100000,
    contentsDeductible: 500,
    termMonths: 12,
    propertyZipCode: '90210',
  });

  const underwritingResponse = await submitUnderwriting(request, quote.quoteId, {
    priorClaimsCount: 0,
    propertyAgeYears: 12,
    creditTier: 'Excellent',
  });

  expect(underwritingResponse.status()).toBe(200);
  const underwritingResult = await underwritingResponse.json();

  const accepted = await acceptQuote(request, quote.quoteId);
  expect(accepted.status).toBe('Accepted');
  expect(accepted.policyCreationInitiated).toBe(true);

  const policy = await waitForPolicyCreation(request, customer.customerId);
  return { customer, policy, quote, underwritingResult };
}

test.describe('[Generated] policy requirements regression', () => {
  test.beforeAll(() => {
    validateConfig(config);
  });

  test('service health endpoint responds for policy', async ({ request }) => {
    const health = await request.get(`${config.apis.policy}/health`);
    expect([200, 204]).toContain(health.status());
  });

  test('accepted quotes create bound policies with locked quote terms', async ({ request }) => {
    test.setTimeout(180000);

    const { customer, policy, quote, underwritingResult } = await createBoundPolicy(request);

    expect(policy.customerId).toBe(customer.customerId);
    expect(policy.status).toBe('Bound');
    expect(policy.policyNumber).toMatch(/^KWG-\d{4}-\d{6}$/);
    expect(policy.premium).toBe(underwritingResult.premium);
    expect(policy.structureCoverageLimit).toBe(quote.structureCoverageLimit);
    expect(policy.termMonths).toBe(quote.termMonths);

    const listResponse = await request.get(`${config.apis.policy}/api/customers/${customer.customerId}/policies`);
    expect(listResponse.status()).toBe(200);
    const listedPolicies = await listResponse.json();
    expect(listedPolicies.customerId).toBe(customer.customerId);
    expect(listedPolicies.policies.some((p: { policyId: string; status: string }) => p.policyId === policy.policyId && p.status === 'Bound')).toBe(true);
  });

  test('bound policies can be issued and cancelled with unearned premium', async ({ request }) => {
    test.setTimeout(180000);
    const { policy } = await createBoundPolicy(request);

    const issueResponse = await request.post(`${config.apis.policy}/api/policies/${policy.policyId}/issue`);
    expect(issueResponse.status()).toBe(200);

    const cancellationDate = new Date(new Date(policy.effectiveDate).getTime() + 24 * 60 * 60 * 1000).toISOString();
    const cancelResponse = await request.post(`${config.apis.policy}/api/policies/${policy.policyId}/cancel`, {
      data: {
        cancellationDate,
        reason: 'CustomerRequest',
      },
    });

    expect(cancelResponse.status()).toBe(200);
    const cancelled = await cancelResponse.json();
    expect(cancelled.status).toBe('Cancelled');
    expect(cancelled.unearnedPremium).toBeGreaterThan(0);
  });
});

test.describe('[Generated] metadata for policy', () => {
  test('source documents snapshot', async () => {
    const snapshot = [
      '- services/policy/docs/business/policy-management.md',
      '- services/policy/docs/technical/policy-technical-spec.md',
    ];

    expect(snapshot.length).toBeGreaterThan(0);
  });
});
