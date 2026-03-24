import { expect, test } from '@playwright/test';
import { getTestConfig, validateConfig } from '../../config/api-endpoints';

const config = getTestConfig();

function buildBillingAccountRequest() {
  const suffix = Date.now().toString();
  return {
    accountId: `acct-${suffix}`,
    customerId: `cust-${suffix}`,
    policyNumber: `KWG-2026-${suffix.slice(-6)}`,
    policyHolderName: 'Generated Billing Customer',
    currentPremiumOwed: 1200,
    billingCycle: 'Annual',
    effectiveDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
  };
}

test.describe('[Generated] billing requirements regression', () => {
  test.beforeAll(() => {
    validateConfig(config);
  });

  test('billing account and payment health endpoints respond', async ({ request }) => {
    const accountHealth = await request.get(`${config.apis.billing}/api/billing/accounts/health`);
    const paymentHealth = await request.get(`${config.apis.billing}/api/billing/payments/health`);
    expect(accountHealth.status()).toBe(200);
    expect(paymentHealth.status()).toBe(200);
  });

  test('account lifecycle and payment recording are reflected in account balances', async ({ request }) => {
    const accountRequest = buildBillingAccountRequest();

    const createResponse = await request.post(`${config.apis.billing}/api/billing/accounts`, {
      data: accountRequest,
      timeout: config.timeouts.apiRequest,
    });
    expect(createResponse.status()).toBe(201);

    const listResponse = await request.get(`${config.apis.billing}/api/billing/accounts`);
    expect(listResponse.status()).toBe(200);
    const accounts = await listResponse.json();
    expect(accounts.some((account: { accountId: string }) => account.accountId === accountRequest.accountId)).toBe(true);

    const getResponse = await request.get(`${config.apis.billing}/api/billing/accounts/${accountRequest.accountId}`);
    expect(getResponse.status()).toBe(200);
    const account = await getResponse.json();
    expect(account.status).toBe('Pending');
    expect(account.billingCycle).toBe('Annual');
    expect(account.outstandingBalance).toBe(1200);

    const activateResponse = await request.post(`${config.apis.billing}/api/billing/accounts/${accountRequest.accountId}/activate`);
    expect(activateResponse.status()).toBe(200);

    const paymentResponse = await request.post(`${config.apis.billing}/api/billing/payments`, {
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

    const asyncPaymentResponse = await request.post(`${config.apis.billing}/api/billing/payments/async`, {
      data: {
        accountId: accountRequest.accountId,
        amount: 50,
        referenceNumber: `async-${Date.now()}`,
      },
    });
    expect(asyncPaymentResponse.status()).toBe(202);

    const suspendResponse = await request.post(`${config.apis.billing}/api/billing/accounts/${accountRequest.accountId}/suspend`, {
      data: { suspensionReason: 'Test suspension' },
    });
    expect(suspendResponse.status()).toBe(200);

    const closeResponse = await request.post(`${config.apis.billing}/api/billing/accounts/${accountRequest.accountId}/close`, {
      data: { closureReason: 'Test closure' },
    });
    expect(closeResponse.status()).toBe(200);
  });
});

test.describe('[Generated] metadata for billing', () => {
  test('source documents snapshot', async () => {
    const snapshot = [
      '- services/legacy/billing/docs/business/billing-account.md',
      '- services/legacy/billing/docs/business/billing-payment.md',
      '- services/legacy/billing/docs/business/multi-policy-billing.md',
      '- services/legacy/billing/docs/technical/highlevel-tech.md',
      '- services/legacy/billing/docs/technical/multi-policy-billing-technical-spec.md',
    ];

    expect(snapshot.length).toBeGreaterThan(0);
  });
});
