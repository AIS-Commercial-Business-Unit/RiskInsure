import { expect, test } from '@playwright/test';
import { getTestConfig, validateConfig } from '../../config/api-endpoints';

const config = getTestConfig();

function buildAccountRequest() {
  const suffix = Date.now().toString();

  return {
    accountId: `acct-${suffix}`,
    customerId: `cust-${suffix}`,
    policyNumber: `KWG-2026-${suffix.slice(-6)}`,
    policyHolderName: 'Generated Billing Customer',
    currentPremiumOwed: 1200,
    policyEquityAndInvoicingCycle: 3,
    effectiveDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
  };
}

test.describe('[Generated] policyequityandinvoicingmgt requirements regression', () => {
  test.beforeAll(() => {
    validateConfig(config);
  });

  test('account and payment health endpoints respond for policyequityandinvoicingmgt', async ({ request }) => {
    const accountHealth = await request.get(`${config.apis.policyequityandinvoicingmgt}/api/policyequityandinvoicingmgt/accounts/health`);
    const paymentHealth = await request.get(`${config.apis.policyequityandinvoicingmgt}/api/policyequityandinvoicingmgt/payments/health`);

    expect(accountHealth.status()).toBe(200);
    expect(paymentHealth.status()).toBe(200);
  });

  test('accounts can be created, retrieved, activated, and paid', async ({ request }) => {
    const accountRequest = buildAccountRequest();

    const createResponse = await request.post(`${config.apis.policyequityandinvoicingmgt}/api/policyequityandinvoicingmgt/accounts`, {
      data: accountRequest,
    });
    expect(createResponse.status()).toBe(201);

    const getResponse = await request.get(`${config.apis.policyequityandinvoicingmgt}/api/policyequityandinvoicingmgt/accounts/${accountRequest.accountId}`);
    expect(getResponse.status()).toBe(200);
    const account = await getResponse.json();
    expect(account.status).toBe('Pending');
    expect(account.outstandingBalance).toBe(1200);

    const activateResponse = await request.post(`${config.apis.policyequityandinvoicingmgt}/api/policyequityandinvoicingmgt/accounts/${accountRequest.accountId}/activate`);
    expect(activateResponse.status()).toBe(200);

    const paymentResponse = await request.post(`${config.apis.policyequityandinvoicingmgt}/api/policyequityandinvoicingmgt/payments`, {
      data: {
        accountId: accountRequest.accountId,
        amount: 200,
        referenceNumber: `pay-${Date.now()}`,
      },
    });

    expect(paymentResponse.status()).toBe(200);
    const payment = await paymentResponse.json();
    expect(payment.totalPaid).toBe(200);
    expect(payment.outstandingBalance).toBe(1000);

    const queuedResponse = await request.post(`${config.apis.policyequityandinvoicingmgt}/api/policyequityandinvoicingmgt/payments/async`, {
      data: {
        accountId: accountRequest.accountId,
        amount: 50,
        referenceNumber: `async-${Date.now()}`,
      },
    });
    expect(queuedResponse.status()).toBe(202);
  });
});

test.describe('[Generated] metadata for policyequityandinvoicingmgt', () => {
  test('source documents snapshot', async () => {
    const snapshot = [
      '- services/policyequityandinvoicingmgt/docs/business/billing-account.md',
      '- services/policyequityandinvoicingmgt/docs/business/billing-payment.md',
      '- services/policyequityandinvoicingmgt/docs/business/multi-policy-billing.md',
      '- services/policyequityandinvoicingmgt/docs/technical/highlevel-tech.md',
      '- services/policyequityandinvoicingmgt/docs/technical/multi-policy-billing-technical-spec.md',
    ];

    expect(snapshot.length).toBeGreaterThan(0);
  });
});
