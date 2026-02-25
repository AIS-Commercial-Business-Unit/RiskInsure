import { test, expect } from '@playwright/test';
import { randomUUID } from 'crypto';

/**
 * Payment Method Lifecycle Integration Tests
 * 
 * Tests the complete workflow of managing payment methods (credit cards and ACH accounts).
 * Requires the Fund Transfer Management API to be running on http://localhost:7073
 * 
 * Run with:
 *   npm test
 *   npm run test:headed (to see browser)
 *   npm run test:debug (debug mode)
 */

test.describe('Payment Method Lifecycle', () => {
  let customerId: string;

  test.beforeEach(() => {
    // Generate unique customer ID for each test run
    customerId = `CUST-${randomUUID()}`;
  });

  test('Add credit card workflow', async ({ request }) => {
    console.log('🚀 Starting test with customerId:', customerId);
    
    const paymentMethodId = randomUUID();
    
    // Step 1: Add credit card
    console.log('📋 Step 1: Adding credit card...');
    const addCardResponse = await request.post(`/api/payment-methods/credit-card`, {
      data: {
        paymentMethodId: paymentMethodId,
        customerId: customerId,
        cardholderName: 'John Doe',
        cardNumber: '4532015112830366', // Valid Visa test card
        expirationMonth: 12,
        expirationYear: 2027,
        cvv: '123',
        billingAddress: {
          street: '123 Main St',
          city: 'Anytown',
          state: 'CA',
          postalCode: '12345',
          country: 'US'
        }
      }
    });
    
    expect(addCardResponse.status()).toBe(201);
    const card = await addCardResponse.json();
    expect(card.customerId).toBe(customerId);
    expect(card.paymentMethodId).toBe(paymentMethodId);
    expect(card.type).toBe('CreditCard');
    expect(card.status).toBe('Validated');
    expect(card.card.brand).toBe('Visa');
    expect(card.card.last4).toBe('0366');
    console.log('✅ Step 1: Credit card added, ID:', card.paymentMethodId);

    // Step 2: Retrieve payment method
    console.log('📋 Step 2: Retrieving payment method...');
    const getResponse = await request.get(`/api/payment-methods/${paymentMethodId}`);
    expect(getResponse.status()).toBe(200);
    
    const retrieved = await getResponse.json();
    expect(retrieved.paymentMethodId).toBe(paymentMethodId);
    expect(retrieved.card.brand).toBe('Visa');
    console.log('✅ Step 2: Payment method retrieved');

    // Step 3: List payment methods for customer
    console.log('📋 Step 3: Listing payment methods for customer...');
    const listResponse = await request.get(`/api/payment-methods?customerId=${customerId}`);
    expect(listResponse.status()).toBe(200);
    
    const paymentMethods = await listResponse.json();
    expect(Array.isArray(paymentMethods)).toBe(true);
    expect(paymentMethods.length).toBeGreaterThan(0);
    
    const ourMethod = paymentMethods.find((pm: any) => pm.paymentMethodId === paymentMethodId);
    expect(ourMethod).toBeDefined();
    console.log('✅ Step 3: Payment method found in list');

    // Step 4: Remove payment method
    console.log('📋 Step 4: Removing payment method...');
    const removeResponse = await request.delete(`/api/payment-methods/${paymentMethodId}`);
    expect(removeResponse.status()).toBe(204);
    console.log('✅ Step 4: Payment method removed');

    // Step 5: Verify payment method is inactive
    console.log('📋 Step 5: Verifying payment method is inactive...');
    const finalGetResponse = await request.get(`/api/payment-methods/${paymentMethodId}`);
    expect(finalGetResponse.status()).toBe(200);
    
    const finalState = await finalGetResponse.json();
    expect(finalState.status).toBe('Inactive');
    console.log('✅ Test completed successfully! Status: Inactive');
  });

  test('Add ACH account workflow', async ({ request }) => {
    console.log('🚀 Starting ACH test with customerId:', customerId);
    
    const paymentMethodId = randomUUID();
    
    // Add ACH account
    console.log('📋 Adding ACH account...');
    const addAchResponse = await request.post(`/api/payment-methods/ach`, {
      data: {
        paymentMethodId: paymentMethodId,
        customerId: customerId,
        accountHolderName: 'Jane Doe',
        routingNumber: '011000015', // Valid test routing number
        accountNumber: '1234567890',
        accountType: 'Checking'
      }
    });
    
    expect(addAchResponse.status()).toBe(201);
    const ach = await addAchResponse.json();
    expect(ach.customerId).toBe(customerId);
    expect(ach.paymentMethodId).toBe(paymentMethodId);
    expect(ach.type).toBe('ACH');
    expect(ach.status).toBe('Validated');
    expect(ach.ach.accountType).toBe('Checking');
    expect(ach.ach.last4).toBe('7890');
    console.log('✅ ACH account added, ID:', ach.paymentMethodId);
  });

  test('Validation - invalid card number', async ({ request }) => {
    console.log('🔍 Testing invalid card number...');
    
    const response = await request.post(`/api/payment-methods/credit-card`, {
      data: {
        paymentMethodId: randomUUID(),
        customerId: customerId,
        cardholderName: 'Test User',
        cardNumber: '1234567890123456', // Invalid Luhn checksum
        expirationMonth: 12,
        expirationYear: 2027,
        cvv: '123',
        billingAddress: {
          street: '123 Main St',
          city: 'Anytown',
          state: 'CA',
          postalCode: '12345',
          country: 'US'
        }
      }
    });
    
    console.log('🔍 Response Status:', response.status());
    expect(response.status()).toBe(400);
    
    const errorBody = await response.json();
    console.log('🔍 Error Response:', JSON.stringify(errorBody, null, 2));
    expect(errorBody.error || errorBody.Error).toContain('Invalid card number');
  });

  test('Validation - empty cardholder name', async ({ request }) => {
    console.log('🔍 Testing empty cardholder name...');
    
    const response = await request.post(`/api/payment-methods/credit-card`, {
      data: {
        paymentMethodId: randomUUID(),
        customerId: customerId,
        cardholderName: '',
        cardNumber: '4532015112830366',
        expirationMonth: 12,
        expirationYear: 2027,
        cvv: '123',
        billingAddress: {
          street: '123 Main St',
          city: 'Anytown',
          state: 'CA',
          postalCode: '12345',
          country: 'US'
        }
      }
    });
    
    expect(response.status()).toBe(400);
    const errorBody = await response.json();
    expect(errorBody.error || errorBody.Error || errorBody.errors?.CardholderName?.[0])
      .toMatch(/cardholder|name/i);
  });

  test('Validation - expired card', async ({ request }) => {
    console.log('🔍 Testing expired card...');
    
    const response = await request.post(`/api/payment-methods/credit-card`, {
      data: {
        paymentMethodId: randomUUID(),
        customerId: customerId,
        cardholderName: 'Test User',
        cardNumber: '4532015112830366',
        expirationMonth: 12,
        expirationYear: 2020, // Expired
        cvv: '123',
        billingAddress: {
          street: '123 Main St',
          city: 'Anytown',
          state: 'CA',
          postalCode: '12345',
          country: 'US'
        }
      }
    });
    
    expect(response.status()).toBe(400);
    const errorBody = await response.json();
    expect(errorBody.error || errorBody.Error).toMatch(/expired/i);
  });

  test('Validation - invalid routing number', async ({ request }) => {
    console.log('🔍 Testing invalid routing number...');
    
    const response = await request.post(`/api/payment-methods/ach`, {
      data: {
        paymentMethodId: randomUUID(),
        customerId: customerId,
        accountHolderName: 'Test User',
        routingNumber: '123456789', // Invalid checksum
        accountNumber: '1234567890',
        accountType: 'Checking'
      }
    });
    
    expect(response.status()).toBe(400);
    const errorBody = await response.json();
    expect(errorBody.error || errorBody.Error).toMatch(/routing/i);
  });
});
