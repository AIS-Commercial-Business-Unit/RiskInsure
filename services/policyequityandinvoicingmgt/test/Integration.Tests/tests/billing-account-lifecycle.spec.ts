/// <reference types="node" />

import { test, expect } from '@playwright/test';
import { randomUUID } from 'node:crypto';

/**
 * Billing Account Lifecycle Integration Tests
 * 
 * Tests the complete workflow of a billing account from creation to closure.
 * Requires the Billing API to be running on http://localhost:7071
 * 
 * Run with:
 *   npm test
 *   npm run test:headed (to see browser)
 *   npm run test:debug (debug mode)
 */

test.describe('Billing Account Lifecycle', () => {
  let accountId: string;
  let customerId: string;
  let policyNumber: string;

  test.beforeEach(() => {
    // Generate unique IDs for each test run
    accountId = randomUUID();
    customerId = `CUST-${Date.now()}`;
    policyNumber = `POL-${Date.now()}`;
  });

  test('Complete billing account workflow', async ({ request }) => {
    console.log('🚀 Starting test with accountId:', accountId);
    
    // Step 1: Verify account doesn't exist (404)
    console.log('📋 Step 1: Checking account does not exist...');
    const getResponse1 = await request.get(`/api/billing/accounts/${accountId}`);
    expect(getResponse1.status()).toBe(404);

    // Step 2: Create billing account
    console.log('📋 Step 2: Creating billing account...');
    const createResponse = await request.post(`/api/billing/accounts`, {
      data: {
        accountId: accountId,
        customerId: customerId,
        policyNumber: policyNumber,
        policyHolderName: 'John Doe',
        currentPremiumOwed: 500.00,
        billingCycle: 'Monthly',
        effectiveDate: '2026-02-01T00:00:00Z'
      }
    });
    
    expect(createResponse.status()).toBe(201);
    const createBody = await createResponse.json();
    expect(createBody.accountId).toBe(accountId);
    expect(createBody.message).toContain('successfully created');
    console.log('✅ Step 2: Account created');

    // Step 3: Verify account exists with correct initial state
    console.log('📋 Step 3: Verifying account initial state...');
    const getResponse2 = await request.get(`/api/billing/accounts/${accountId}`);
    expect(getResponse2.status()).toBe(200);
    
    const account1 = await getResponse2.json();
    expect(account1.accountId).toBe(accountId);
    expect(account1.status).toBe('Pending');
    expect(account1.currentPremiumOwed).toBe(500.00);
    expect(account1.totalPaid).toBe(0);
    expect(account1.outstandingBalance).toBe(500.00);
    console.log('✅ Step 3: Account state verified');

    // Step 4: Activate account
    console.log('📋 Step 4: Activating account...');
    const activateResponse = await request.post(`/api/billing/accounts/${accountId}/activate`);
    expect(activateResponse.status()).toBe(200);
    console.log('✅ Step 4: Account activated');

    // Step 5: Verify account is now Active
    console.log('📋 Step 5: Verifying Active status...');
    const getResponse3 = await request.get(`/api/billing/accounts/${accountId}`);
    expect(getResponse3.status()).toBe(200);
    
    const account2 = await getResponse3.json();
    expect(account2.status).toBe('Active');
    console.log('✅ Step 5: Status is Active');

    // Step 6: Record first payment ($150)
    console.log('📋 Step 6: Recording first payment ($150)...');
    const payment1Response = await request.post(`/api/billing/payments`, {
      data: {
        accountId: accountId,
        amount: 150.00,
        referenceNumber: `PAY-${Date.now()}-1`,
        paymentDate: new Date().toISOString()
      }
    });
    
    expect(payment1Response.status()).toBe(200);
    console.log('✅ Step 6: First payment recorded');

    // Step 7: Verify payment was applied
    console.log('📋 Step 7: Verifying first payment applied...');
    const getResponse4 = await request.get(`/api/billing/accounts/${accountId}`);
    expect(getResponse4.status()).toBe(200);
    
    const account3 = await getResponse4.json();
    expect(account3.totalPaid).toBe(150.00);
    expect(account3.outstandingBalance).toBe(350.00);
    console.log('✅ Step 7: Payment verified - Balance: $350');

    // Step 8: Record second payment ($200)
    console.log('📋 Step 8: Recording second payment ($200)...');
    const payment2Response = await request.post(`/api/billing/payments`, {
      data: {
        accountId: accountId,
        amount: 200.00,
        referenceNumber: `PAY-${Date.now()}-2`,
        paymentDate: new Date().toISOString()
      }
    });
    
    expect(payment2Response.status()).toBe(200);
    console.log('✅ Step 8: Second payment recorded');

    // Step 9: Verify total payments
    console.log('📋 Step 9: Verifying total payments...');
    const getResponse5 = await request.get(`/api/billing/accounts/${accountId}`);
    expect(getResponse5.status()).toBe(200);
    
    const account4 = await getResponse5.json();
    expect(account4.totalPaid).toBe(350.00);
    expect(account4.outstandingBalance).toBe(150.00);
    console.log('✅ Step 9: Total payments verified - Balance: $150');

    // Step 10: Update premium
    console.log('📋 Step 10: Updating premium to $600...');
    const updatePremiumResponse = await request.put(`/api/billing/accounts/${accountId}/premium`, {
      data: {
        newPremiumOwed: 600.00,
        changeReason: 'Premium increase due to policy change'
      }
    });
    
    expect(updatePremiumResponse.status()).toBe(200);
    console.log('✅ Step 10: Premium updated');

    // Verify premium was updated
    console.log('📋 Step 10b: Verifying premium change...');
    const getResponse6 = await request.get(`/api/billing/accounts/${accountId}`);
    const account5 = await getResponse6.json();
    expect(account5.currentPremiumOwed).toBe(600.00);
    expect(account5.outstandingBalance).toBe(250.00); // 600 - 350 paid
    console.log('✅ Step 10b: Premium verified - New Balance: $250');

    // Step 11: Suspend account
    console.log('📋 Step 11: Suspending account...');
    const suspendResponse = await request.post(`/api/billing/accounts/${accountId}/suspend`, {
      data: {
        suspensionReason: 'Non-payment'
      }
    });
    
    expect(suspendResponse.status()).toBe(200);
    console.log('✅ Step 11: Account suspended');

    // Verify status is Suspended
    console.log('📋 Step 11b: Verifying Suspended status...');
    const getResponse7 = await request.get(`/api/billing/accounts/${accountId}`);
    const account6 = await getResponse7.json();
    expect(account6.status).toBe('Suspended');
    console.log('✅ Step 11b: Status is Suspended');

    // Step 12: Close account
    console.log('📋 Step 12: Closing account...');
    const closeResponse = await request.post(`/api/billing/accounts/${accountId}/close`, {
      data: {
        closureReason: 'Policy terminated'
      }
    });
    
    expect(closeResponse.status()).toBe(200);
    console.log('✅ Step 12: Account closed');

    // Final verification
    console.log('📋 Step 13: Final verification...');
    const getResponse8 = await request.get(`/api/billing/accounts/${accountId}`);
    const finalAccount = await getResponse8.json();
    expect(finalAccount.status).toBe('Closed');
    console.log('✅ Test completed successfully! Status: Closed');
  });

  test('Get all accounts includes created account', async ({ request }) => {
    // Create an account
    await request.post(`/api/billing/accounts`, {
      data: {
        accountId: accountId,
        customerId: customerId,
        policyNumber: policyNumber,
        policyHolderName: 'Jane Doe',
        currentPremiumOwed: 300.00,
        billingCycle: 'Quarterly',
        effectiveDate: '2026-02-01T00:00:00Z'
      }
    });

    // Get all accounts
    const getAllResponse = await request.get(`/api/billing/accounts`);
    expect(getAllResponse.status()).toBe(200);
    
    const accounts = await getAllResponse.json();
    expect(Array.isArray(accounts)).toBe(true);
    
    // Find our account in the list
    const ourAccount = accounts.find((acc: any) => acc.accountId === accountId);
    expect(ourAccount).toBeDefined();
    expect(ourAccount.policyHolderName).toBe('Jane Doe');
  });

  test('Payment validation - negative amount', async ({ request }) => {
    // Create and activate account
    await request.post(`/api/billing/accounts`, {
      data: {
        accountId: accountId,
        customerId: customerId,
        policyNumber: policyNumber,
        policyHolderName: 'Test User',
        currentPremiumOwed: 500.00,
        billingCycle: 'Monthly',
        effectiveDate: '2026-02-01T00:00:00Z'
      }
    });
    
    await request.post(`/api/billing/accounts/${accountId}/activate`);

    // Try to record negative payment
    const paymentResponse = await request.post(`/api/billing/payments`, {
      data: {
        accountId: accountId,
        amount: -50.00,
        referenceNumber: `PAY-NEGATIVE`,
        paymentDate: new Date().toISOString()
      }
    });
    
    console.log('🔍 Payment Response Status:', paymentResponse.status());
    console.log('🔍 Payment Response Headers:', paymentResponse.headers());
    const errorBody = await paymentResponse.json();
    console.log('🔍 Payment Response Body:', JSON.stringify(errorBody, null, 2));
    
    expect(paymentResponse.status()).toBe(400);
    // ModelState returns: { errors: { Amount: ["message"] } }
    expect(errorBody.errors?.Amount?.[0] || errorBody.error || errorBody.Error).toContain('greater than zero');
  });

  test('Payment validation - exceeds balance', async ({ request }) => {
    // Create and activate account
    await request.post(`/api/billing/accounts`, {
      data: {
        accountId: accountId,
        customerId: customerId,
        policyNumber: policyNumber,
        policyHolderName: 'Test User',
        currentPremiumOwed: 100.00,
        billingCycle: 'Monthly',
        effectiveDate: '2026-02-01T00:00:00Z'
      }
    });
    
    await request.post(`/api/billing/accounts/${accountId}/activate`);

    // Try to pay more than owed
    const paymentResponse = await request.post(`/api/billing/payments`, {
      data: {
        accountId: accountId,
        amount: 150.00, // More than $100 owed
        referenceNumber: `PAY-OVERPAY`,
        paymentDate: new Date().toISOString()
      }
    });
    
    console.log('🔍 Overpay Response Status:', paymentResponse.status());
    console.log('🔍 Overpay Response Headers:', paymentResponse.headers());
    const errorBody = await paymentResponse.json();
    console.log('🔍 Overpay Response Body:', JSON.stringify(errorBody, null, 2));
    
    expect(paymentResponse.status()).toBe(400);
    // Business validation returns: { Error: "message" }
    expect(errorBody.Error || errorBody.error).toContain('exceeds');
  });

  test('Cannot record payment on inactive account', async ({ request }) => {
    // Create account but DON'T activate
    await request.post(`/api/billing/accounts`, {
      data: {
        accountId: accountId,
        customerId: customerId,
        policyNumber: policyNumber,
        policyHolderName: 'Test User',
        currentPremiumOwed: 500.00,
        billingCycle: 'Monthly',
        effectiveDate: '2026-02-01T00:00:00Z'
      }
    });

    // Try to record payment on Pending account
    const paymentResponse = await request.post(`/api/billing/payments`, {
      data: {
        accountId: accountId,
        amount: 100.00,
        referenceNumber: `PAY-INACTIVE`,
        paymentDate: new Date().toISOString()
      }
    });
    
    expect(paymentResponse.status()).toBe(400);
    const errorBody = await paymentResponse.json();
    // Business validation returns: { Error: "message" }
    expect(errorBody.Error || errorBody.error).toContain('status');
  });
});
