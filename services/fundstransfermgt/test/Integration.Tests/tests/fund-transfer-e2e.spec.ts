import { test, expect } from '@playwright/test';
import { randomUUID } from 'crypto';

/**
 * End-to-End Fund Transfer Integration Tests
 * 
 * Tests the complete workflow of fund transfers including:
 * 1. Creating payment methods (credit card and ACH)
 * 2. Initiating fund transfers
 * 3. Validating transfer status and details
 * 4. Testing error scenarios
 * 
 * Requires the Fund Transfer Management API to be running on http://localhost:7073
 * 
 * Run with:
 *   npm test fund-transfer-e2e
 *   npm run test:headed -- fund-transfer-e2e (to see browser)
 *   npm run test:ui (interactive mode)
 */

test.describe('Fund Transfer End-to-End Workflow', () => {
  let customerId: string;

  test.beforeEach(() => {
    // Generate unique customer ID for each test run to avoid conflicts
    customerId = `CUST-${randomUUID()}`;
  });

  test('Complete credit card fund transfer workflow', async ({ request }) => {
    console.log('🚀 Starting E2E test with customerId:', customerId);
    
    // ==========================================
    // STEP 1: Add Credit Card Payment Method
    // ==========================================
    console.log('\n📋 STEP 1: Adding credit card payment method...');
    const paymentMethodId = randomUUID();
    
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
          city: 'San Francisco',
          state: 'CA',
          postalCode: '94102',
          country: 'US'
        }
      }
    });
    
    expect(addCardResponse.status()).toBe(201);
    const creditCard = await addCardResponse.json();
    
    expect(creditCard.customerId).toBe(customerId);
    expect(creditCard.paymentMethodId).toBe(paymentMethodId);
    expect(creditCard.type).toBe('CreditCard');
    expect(creditCard.status).toBe('Validated');
    expect(creditCard.card.brand).toBe('Visa');
    expect(creditCard.card.last4).toBe('0366');
    expect(creditCard.card.cardholderName).toBe('John Doe');
    
    console.log('✅ Credit card added successfully');
    console.log(`   Payment Method ID: ${paymentMethodId}`);
    console.log(`   Card: Visa ending in ${creditCard.card.last4}`);
    console.log(`   Status: ${creditCard.status}`);

    // ==========================================
    // STEP 2: Verify Payment Method Retrieval
    // ==========================================
    console.log('\n📋 STEP 2: Verifying payment method retrieval...');
    
    const getPaymentMethodResponse = await request.get(`/api/payment-methods/${paymentMethodId}`);
    expect(getPaymentMethodResponse.status()).toBe(200);
    
    const retrievedPaymentMethod = await getPaymentMethodResponse.json();
    expect(retrievedPaymentMethod.paymentMethodId).toBe(paymentMethodId);
    expect(retrievedPaymentMethod.status).toBe('Validated');
    
    console.log('✅ Payment method retrieved successfully');

    // ==========================================
    // STEP 3: Initiate Fund Transfer
    // ==========================================
    console.log('\n📋 STEP 3: Initiating fund transfer...');
    
    const transferAmount = 150.00;
    const transferPurpose = 'E2E Test - Premium Payment';
    
    const transferResponse = await request.post(`/api/fund-transfers`, {
      data: {
        customerId: customerId,
        paymentMethodId: paymentMethodId,
        amount: transferAmount,
        purpose: transferPurpose
      }
    });
    
    expect(transferResponse.status()).toBe(200);
    const transfer = await transferResponse.json();
    
    expect(transfer.customerId).toBe(customerId);
    expect(transfer.paymentMethodId).toBe(paymentMethodId);
    expect(transfer.amount).toBe(transferAmount);
    expect(transfer.purpose).toBe(transferPurpose);
    expect(transfer.direction).toBe('Inbound');
    expect(transfer.status).toBe('Settled'); // Mock gateway settles immediately
    expect(transfer.transactionId).toBeTruthy();
    expect(transfer.initiatedUtc).toBeTruthy();
    expect(transfer.settledUtc).toBeTruthy();
    
    console.log('✅ Fund transfer initiated and settled successfully');
    console.log(`   Transaction ID: ${transfer.transactionId}`);
    console.log(`   Amount: $${transfer.amount}`);
    console.log(`   Status: ${transfer.status}`);
    console.log(`   Purpose: ${transfer.purpose}`);

    // ==========================================
    // STEP 4: Retrieve Transfer by ID
    // ==========================================
    console.log('\n📋 STEP 4: Retrieving transfer by ID...');
    
    const getTransferResponse = await request.get(`/api/fund-transfers/${transfer.transactionId}`);
    expect(getTransferResponse.status()).toBe(200);
    
    const retrievedTransfer = await getTransferResponse.json();
    expect(retrievedTransfer.transactionId).toBe(transfer.transactionId);
    expect(retrievedTransfer.customerId).toBe(customerId);
    expect(retrievedTransfer.amount).toBe(transferAmount);
    expect(retrievedTransfer.status).toBe('Settled');
    
    console.log('✅ Transfer retrieved successfully by ID');

    // ==========================================
    // STEP 5: Get Customer Transfer History
    // ==========================================
    console.log('\n📋 STEP 5: Retrieving customer transfer history...');
    
    const historyResponse = await request.get(`/api/fund-transfers?customerId=${customerId}`);
    expect(historyResponse.status()).toBe(200);
    
    const transfers = await historyResponse.json();
    expect(Array.isArray(transfers)).toBe(true);
    expect(transfers.length).toBeGreaterThan(0);
    
    const ourTransfer = transfers.find((t: any) => t.transactionId === transfer.transactionId);
    expect(ourTransfer).toBeDefined();
    expect(ourTransfer.amount).toBe(transferAmount);
    
    console.log('✅ Customer transfer history retrieved');
    console.log(`   Total transfers: ${transfers.length}`);

    // ==========================================
    // STEP 6: Verify Multiple Transfers
    // ==========================================
    console.log('\n📋 STEP 6: Creating second transfer to verify history...');
    
    const secondTransferResponse = await request.post(`/api/fund-transfers`, {
      data: {
        customerId: customerId,
        paymentMethodId: paymentMethodId,
        amount: 75.50,
        purpose: 'E2E Test - Additional Payment'
      }
    });
    
    expect(secondTransferResponse.status()).toBe(200);
    const secondTransfer = await secondTransferResponse.json();
    expect(secondTransfer.status).toBe('Settled');
    
    const updatedHistoryResponse = await request.get(`/api/fund-transfers?customerId=${customerId}`);
    const updatedTransfers = await updatedHistoryResponse.json();
    expect(updatedTransfers.length).toBe(2);
    
    console.log('✅ Second transfer created successfully');
    console.log(`   Transaction count for customer: ${updatedTransfers.length}`);

    console.log('\n🎉 E2E test completed successfully!');
  });

  test('Complete ACH fund transfer workflow', async ({ request }) => {
    console.log('🚀 Starting ACH E2E test with customerId:', customerId);
    
    // ==========================================
    // STEP 1: Add ACH Payment Method
    // ==========================================
    console.log('\n📋 STEP 1: Adding ACH payment method...');
    const paymentMethodId = randomUUID();
    
    const addAchResponse = await request.post(`/api/payment-methods/ach`, {
      data: {
        paymentMethodId: paymentMethodId,
        customerId: customerId,
        accountHolderName: 'Jane Smith',
        routingNumber: '011000015', // Valid test routing number
        accountNumber: '1234567890',
        accountType: 'Checking'
      }
    });
    
    expect(addAchResponse.status()).toBe(201);
    const achAccount = await addAchResponse.json();
    
    expect(achAccount.customerId).toBe(customerId);
    expect(achAccount.paymentMethodId).toBe(paymentMethodId);
    expect(achAccount.type).toBe('ACH');
    expect(achAccount.status).toBe('Validated');
    expect(achAccount.ach.accountType).toBe('Checking');
    expect(achAccount.ach.last4).toBe('7890');
    
    console.log('✅ ACH account added successfully');
    console.log(`   Payment Method ID: ${paymentMethodId}`);
    console.log(`   Account: Checking ending in ${achAccount.ach.last4}`);

    // ==========================================
    // STEP 2: Initiate Fund Transfer with ACH
    // ==========================================
    console.log('\n📋 STEP 2: Initiating fund transfer with ACH...');
    
    const transferAmount = 500.00;
    
    const transferResponse = await request.post(`/api/fund-transfers`, {
      data: {
        customerId: customerId,
        paymentMethodId: paymentMethodId,
        amount: transferAmount,
        purpose: 'ACH E2E Test - Large Payment'
      }
    });
    
    expect(transferResponse.status()).toBe(200);
    const transfer = await transferResponse.json();
    
    expect(transfer.customerId).toBe(customerId);
    expect(transfer.paymentMethodId).toBe(paymentMethodId);
    expect(transfer.amount).toBe(transferAmount);
    expect(transfer.status).toBe('Settled');
    
    console.log('✅ ACH fund transfer completed successfully');
    console.log(`   Transaction ID: ${transfer.transactionId}`);
    console.log(`   Amount: $${transfer.amount}`);

    console.log('\n🎉 ACH E2E test completed successfully!');
  });

  test('Error handling - transfer with invalid payment method', async ({ request }) => {
    console.log('🔍 Testing error handling for invalid payment method...');
    
    const invalidPaymentMethodId = randomUUID();
    
    const transferResponse = await request.post(`/api/fund-transfers`, {
      data: {
        customerId: customerId,
        paymentMethodId: invalidPaymentMethodId,
        amount: 100.00,
        purpose: 'Should fail - invalid payment method'
      }
    });
    
    expect(transferResponse.status()).toBe(400);
    const error = await transferResponse.json();
    expect(error.error).toBeTruthy();
    expect(error.error).toContain('not found');
    
    console.log('✅ Invalid payment method correctly rejected');
  });

  test('Error handling - transfer with inactive payment method', async ({ request }) => {
    console.log('🔍 Testing error handling for inactive payment method...');
    
    // Create payment method
    const paymentMethodId = randomUUID();
    await request.post(`/api/payment-methods/credit-card`, {
      data: {
        paymentMethodId: paymentMethodId,
        customerId: customerId,
        cardholderName: 'Test User',
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
    
    // Remove payment method (makes it inactive)
    await request.delete(`/api/payment-methods/${paymentMethodId}`);
    
    // Try to use inactive payment method
    const transferResponse = await request.post(`/api/fund-transfers`, {
      data: {
        customerId: customerId,
        paymentMethodId: paymentMethodId,
        amount: 100.00,
        purpose: 'Should fail - inactive payment method'
      }
    });
    
    expect(transferResponse.status()).toBe(400);
    const error = await transferResponse.json();
    expect(error.error).toBeTruthy();
    expect(error.error).toContain('not active');
    
    console.log('✅ Inactive payment method correctly rejected');
  });

  test('Error handling - transfer with wrong customer payment method', async ({ request }) => {
    console.log('🔍 Testing error handling for payment method belonging to different customer...');
    
    const otherCustomerId = `CUST-OTHER-${randomUUID()}`;
    const paymentMethodId = randomUUID();
    
    // Create payment method for different customer
    await request.post(`/api/payment-methods/credit-card`, {
      data: {
        paymentMethodId: paymentMethodId,
        customerId: otherCustomerId,
        cardholderName: 'Other User',
        cardNumber: '4532015112830366',
        expirationMonth: 12,
        expirationYear: 2027,
        cvv: '123',
        billingAddress: {
          street: '456 Other St',
          city: 'Othertown',
          state: 'NY',
          postalCode: '67890',
          country: 'US'
        }
      }
    });
    
    // Try to use payment method with different customer ID
    const transferResponse = await request.post(`/api/fund-transfers`, {
      data: {
        customerId: customerId, // Different customer
        paymentMethodId: paymentMethodId, // Belongs to otherCustomerId
        amount: 100.00,
        purpose: 'Should fail - wrong customer'
      }
    });
    
    expect(transferResponse.status()).toBe(400);
    const error = await transferResponse.json();
    expect(error.error).toBeTruthy();
    expect(error.error).toContain('does not belong to customer');
    
    console.log('✅ Cross-customer payment method correctly rejected');
  });

  test('Multi-payment method scenario', async ({ request }) => {
    console.log('🚀 Testing multiple payment methods for one customer...');
    
    // Add credit card
    const creditCardId = randomUUID();
    const creditCardResponse = await request.post(`/api/payment-methods/credit-card`, {
      data: {
        paymentMethodId: creditCardId,
        customerId: customerId,
        cardholderName: 'John Doe',
        cardNumber: '4532015112830366',
        expirationMonth: 6,
        expirationYear: 2028,
        cvv: '456',
        billingAddress: {
          street: '789 Card St',
          city: 'Cardtown',
          state: 'TX',
          postalCode: '75001',
          country: 'US'
        }
      }
    });
    expect(creditCardResponse.status()).toBe(201);
    
    // Add ACH account
    const achId = randomUUID();
    const achResponse = await request.post(`/api/payment-methods/ach`, {
      data: {
        paymentMethodId: achId,
        customerId: customerId,
        accountHolderName: 'John Doe',
        routingNumber: '011000015',
        accountNumber: '9876543210',
        accountType: 'Savings'
      }
    });
    expect(achResponse.status()).toBe(201);
    
    console.log('✅ Added both credit card and ACH payment methods');
    
    // Transfer with credit card
    const ccTransferResponse = await request.post(`/api/fund-transfers`, {
      data: {
        customerId: customerId,
        paymentMethodId: creditCardId,
        amount: 250.00,
        purpose: 'Credit card payment'
      }
    });
    expect(ccTransferResponse.status()).toBe(200);
    const ccTransfer = await ccTransferResponse.json();
    expect(ccTransfer.paymentMethodId).toBe(creditCardId);
    
    console.log('✅ Credit card transfer completed');
    
    // Transfer with ACH
    const achTransferResponse = await request.post(`/api/fund-transfers`, {
      data: {
        customerId: customerId,
        paymentMethodId: achId,
        amount: 1000.00,
        purpose: 'ACH payment'
      }
    });
    expect(achTransferResponse.status()).toBe(200);
    const achTransfer = await achTransferResponse.json();
    expect(achTransfer.paymentMethodId).toBe(achId);
    
    console.log('✅ ACH transfer completed');
    
    // Verify both transfers in history
    const historyResponse = await request.get(`/api/fund-transfers?customerId=${customerId}`);
    const transfers = await historyResponse.json();
    expect(transfers.length).toBe(2);
    
    const ccTransferInHistory = transfers.find((t: any) => t.transactionId === ccTransfer.transactionId);
    const achTransferInHistory = transfers.find((t: any) => t.transactionId === achTransfer.transactionId);
    
    expect(ccTransferInHistory).toBeDefined();
    expect(achTransferInHistory).toBeDefined();
    
    console.log('✅ Both transfers found in customer history');
    console.log(`   Credit card: $${ccTransferInHistory.amount}`);
    console.log(`   ACH: $${achTransferInHistory.amount}`);
    
    console.log('\n🎉 Multi-payment method test completed successfully!');
  });

  test('Large amount transfer validation', async ({ request }) => {
    console.log('🔍 Testing large amount transfer...');
    
    const paymentMethodId = randomUUID();
    await request.post(`/api/payment-methods/credit-card`, {
      data: {
        paymentMethodId: paymentMethodId,
        customerId: customerId,
        cardholderName: 'High Roller',
        cardNumber: '4532015112830366',
        expirationMonth: 12,
        expirationYear: 2027,
        cvv: '999',
        billingAddress: {
          street: '999 Rich St',
          city: 'Wealthville',
          state: 'CA',
          postalCode: '90210',
          country: 'US'
        }
      }
    });
    
    const largeAmount = 10000.00;
    const transferResponse = await request.post(`/api/fund-transfers`, {
      data: {
        customerId: customerId,
        paymentMethodId: paymentMethodId,
        amount: largeAmount,
        purpose: 'Large premium payment'
      }
    });
    
    expect(transferResponse.status()).toBe(200);
    const transfer = await transferResponse.json();
    expect(transfer.amount).toBe(largeAmount);
    expect(transfer.status).toBe('Settled');
    
    console.log('✅ Large amount transfer processed successfully');
    console.log(`   Amount: $${transfer.amount.toLocaleString()}`);
  });

  test('Retrieve non-existent transfer returns 404', async ({ request }) => {
    console.log('🔍 Testing retrieval of non-existent transfer...');
    
    const fakeTransactionId = randomUUID();
    const response = await request.get(`/api/fund-transfers/${fakeTransactionId}`);
    
    expect(response.status()).toBe(404);
    
    console.log('✅ Non-existent transfer correctly returns 404');
  });
});
