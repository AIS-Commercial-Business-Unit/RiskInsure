import { expect, test } from '@playwright/test';
import { getTestConfig, validateConfig } from '../../config/api-endpoints';

const config = getTestConfig();

test.describe('[Generated] fundstransfermgt requirements regression', () => {
  test.beforeAll(() => {
    validateConfig(config);
  });

  test('service health endpoint responds for fundstransfermgt', async ({ request }) => {
    const health = await request.get(`${config.apis.fundsTransfer}/health`);
    expect([200, 204]).toContain(health.status());
  });

  test('payment method and transfer workflow is queryable by id and customer', async ({ request }) => {
    const suffix = Date.now().toString();
    const customerId = `cust-ft-${suffix}`;
    const paymentMethodId = `pm-${suffix}`;

    const addCardResponse = await request.post(`${config.apis.fundsTransfer}/api/payment-methods/credit-card`, {
      data: {
        paymentMethodId,
        customerId,
        cardholderName: 'Test Customer',
        cardNumber: '4111111111111111',
        expirationMonth: 12,
        expirationYear: new Date().getUTCFullYear() + 2,
        cvv: '123',
        billingAddress: {
          street: '1 Payment Way',
          city: 'Austin',
          state: 'TX',
          postalCode: '73301',
          country: 'US',
        },
      },
      timeout: config.timeouts.apiRequest,
    });

    expect(addCardResponse.status()).toBe(201);
    const paymentMethod = await addCardResponse.json();
    expect(paymentMethod.paymentMethodId).toBe(paymentMethodId);
    expect(paymentMethod.customerId).toBe(customerId);
    expect(paymentMethod.type).toBe('CreditCard');

    const transferResponse = await request.post(`${config.apis.fundsTransfer}/api/fund-transfers`, {
      data: {
        customerId,
        paymentMethodId,
        amount: 125.5,
        purpose: 'PremiumPayment',
      },
      timeout: config.timeouts.apiRequest,
    });

    expect([200, 202]).toContain(transferResponse.status());
    const transfer = await transferResponse.json();
    const transactionId = transfer.transactionId ?? transfer.transferId;
    expect(transfer.customerId).toBe(customerId);
    expect(transfer.paymentMethodId).toBe(paymentMethodId);
    expect(transfer.amount).toBe(125.5);
    expect(transactionId).toBeTruthy();

    const getTransferResponse = await request.get(`${config.apis.fundsTransfer}/api/fund-transfers/${transactionId}`);
    expect(getTransferResponse.status()).toBe(200);
    const fetchedTransfer = await getTransferResponse.json();
    expect(fetchedTransfer.transactionId).toBe(transactionId);

    const listTransfersResponse = await request.get(`${config.apis.fundsTransfer}/api/fund-transfers?customerId=${customerId}`);
    expect(listTransfersResponse.status()).toBe(200);
    const customerTransfers = await listTransfersResponse.json();
    expect(Array.isArray(customerTransfers)).toBe(true);
    expect(customerTransfers.some((item: { transactionId: string }) => item.transactionId === transactionId)).toBe(true);

    const listPaymentMethodsResponse = await request.get(`${config.apis.fundsTransfer}/api/payment-methods?customerId=${customerId}`);
    expect(listPaymentMethodsResponse.status()).toBe(200);
    const paymentMethods = await listPaymentMethodsResponse.json();
    expect(Array.isArray(paymentMethods)).toBe(true);
    expect(paymentMethods.some((item: { paymentMethodId: string }) => item.paymentMethodId === paymentMethodId)).toBe(true);
  });
});

test.describe('[Generated] metadata for fundstransfermgt', () => {
  test('source documents snapshot', async () => {
    const snapshot = [
      '- services/fundstransfermgt/docs/business/domain-context.md',
      '- services/fundstransfermgt/docs/business/fund-transfers.md',
      '- services/fundstransfermgt/docs/business/payment-methods.md',
      '- services/fundstransfermgt/docs/technical/fund-transfers-tech.md',
      '- services/fundstransfermgt/docs/technical/highlevel-tech.md',
      '- services/fundstransfermgt/docs/technical/payment-methods-tech.md',
    ];

    expect(snapshot.length).toBeGreaterThan(0);
  });
});
