import { test, expect } from '@playwright/test';
import { getTestConfig, validateConfig } from '../config/api-endpoints';
import { createCustomer } from '../helpers/customer-api';
import { startQuote, submitUnderwriting, acceptQuote, getQuote } from '../helpers/rating-api';
import { waitForPolicyCreation } from '../helpers/policy-api';

const config = getTestConfig();

test.describe('Quote to Policy Flow', () => {
  test.beforeAll(() => {
    // Validate configuration before running tests
    validateConfig(config);
    console.log('E2E Test Configuration:', {
      customer: config.apis.customer,
      rating: config.apis.rating,
      policy: config.apis.policy,
      eventualConsistencyTimeout: config.timeouts.eventualConsistency,
    });
  });

  test('complete quote to policy workflow with Class A approval', async ({ request }) => {
    // Step 1: Create Customer (Customer Domain - 7073)
    const customer = await createCustomer(request, {
      firstName: 'Alice',
      lastName: 'Johnson',
      address: {
        street: '456 Oak Avenue',
        city: 'Beverly Hills',
        state: 'CA',
        zipCode: '90210',
      },
    });

    console.log(`✓ Customer created: ${customer.customerId}`);
    expect(customer.customerId).toBeTruthy();
    expect(customer.firstName).toBe('Alice');
    expect(customer.lastName).toBe('Johnson');

    // Step 2: Start Quote (Rating & Underwriting Domain - 7079)
    const quote = await startQuote(request, customer.customerId, {
      structureCoverageLimit: 300000,
      structureDeductible: 1000,
      contentsCoverageLimit: 100000,
      contentsDeductible: 500,
      termMonths: 12,
      propertyZipCode: '90210', // Zone 1 territory (0.90 factor)
    });

    console.log(`✓ Quote started: ${quote.quoteId}`);
    expect(quote.quoteId).toBeTruthy();
    expect(quote.customerId).toBe(customer.customerId);
    expect(quote.status).toBe('Draft');

    // Step 3: Submit Underwriting (Rating & Underwriting Domain - 7079)
    const underwritingResponse = await submitUnderwriting(request, quote.quoteId, {
      priorClaimsCount: 0, // Class A: 0 claims
      propertyAgeYears: 10, // Class A: ≤15 years
      creditTier: 'Excellent', // Class A: Excellent credit
    });
    expect(underwritingResponse.status()).toBe(200);
    const underwritingResult = await underwritingResponse.json();

    console.log(`✓ Underwriting submitted: Class ${underwritingResult.underwritingClass}, Premium: $${underwritingResult.premium}`);
    expect(underwritingResult.underwritingClass).toBe('A');
    expect(underwritingResult.status).toBe('Quoted');
    expect(underwritingResult.premium).toBeGreaterThan(0);
    expect(underwritingResult.expirationUtc).toBeTruthy();

    // Verify quote details after underwriting
    const quotedQuote = await getQuote(request, quote.quoteId);
    expect(quotedQuote.status).toBe('Quoted');
    expect(quotedQuote.premium).toBe(underwritingResult.premium);

    // Step 4: Accept Quote (Rating & Underwriting Domain - 7079)
    const acceptResult = await acceptQuote(request, quote.quoteId);

    console.log(`✓ Quote accepted: ${acceptResult.quoteId}`);
    expect(acceptResult.status).toBe('Accepted');
    expect(acceptResult.policyCreationInitiated).toBe(true);
    expect(acceptResult.acceptedUtc).toBeTruthy();

    // Step 5: Wait for Policy Creation (Policy Domain - 7077)
    // This handles eventual consistency - QuoteAccepted event → PolicyCreated
    console.log(`⏳ Waiting for policy creation (max ${config.timeouts.eventualConsistency}ms)...`);
    
    const policy = await waitForPolicyCreation(request, customer.customerId);

    console.log(`✓ Policy created: ${policy.policyNumber} (${policy.policyId})`);
    
    // Step 6: Verify Policy Details Match Quote
    expect(policy.customerId).toBe(customer.customerId);
    expect(policy.status).toBe('Bound');
    expect(policy.premium).toBe(underwritingResult.premium);
    expect(policy.structureCoverageLimit).toBe(quote.structureCoverageLimit);
    expect(policy.structureDeductible).toBe(quote.structureDeductible);
    expect(policy.contentsCoverageLimit).toBe(quote.contentsCoverageLimit);
    expect(policy.contentsDeductible).toBe(quote.contentsDeductible);
    expect(policy.termMonths).toBe(quote.termMonths);
    expect(policy.policyNumber).toMatch(/^KWG-\d{4}-\d{6}$/); // Format: KWG-YYYY-NNNNNN
    console.log('\n✅ Complete Quote-to-Policy Flow Successful!');
    console.log(`   Customer: ${customer.firstName} ${customer.lastName} (${customer.customerId})`);
    console.log(`   Quote: ${quote.quoteId} (Class A, $${underwritingResult.premium})`);
    console.log(`   Policy: ${policy.policyNumber} (${policy.status})`);
  });

  test('complete quote to policy workflow with Class B approval', async ({ request }) => {
    // Step 1: Create Customer
    const customer = await createCustomer(request, {
      firstName: 'Bob',
      lastName: 'Smith',
    });

    console.log(`✓ Customer created: ${customer.customerId}`);

    // Step 2: Start Quote
    const quote = await startQuote(request, customer.customerId);
    console.log(`✓ Quote started: ${quote.quoteId}`);

    // Step 3: Submit Underwriting with Class B criteria
    const underwritingResponse = await submitUnderwriting(request, quote.quoteId, {
      priorClaimsCount: 1, // Class B: 0-1 claims
      propertyAgeYears: 25, // Class B: ≤30 years
      creditTier: 'Good', // Class B: Good or Excellent
    });
    expect(underwritingResponse.status()).toBe(200);
    const underwritingResult = await underwritingResponse.json();

    console.log(`✓ Underwriting submitted: Class ${underwritingResult.underwritingClass}, Premium: $${underwritingResult.premium}`);
    expect(underwritingResult.underwritingClass).toBe('B');
    expect(underwritingResult.status).toBe('Quoted');
    expect(underwritingResult.premium).toBeGreaterThan(0);

    // Step 4: Accept Quote
    const acceptResult = await acceptQuote(request, quote.quoteId);
    console.log(`✓ Quote accepted: ${acceptResult.quoteId}`);

    // Step 5: Wait for Policy Creation
    console.log(`⏳ Waiting for policy creation...`);
    const policy = await waitForPolicyCreation(request, customer.customerId);
    console.log(`✓ Policy created: ${policy.policyNumber}`);

    // Step 6: Verify
    expect(policy.customerId).toBe(customer.customerId);
    expect(policy.status).toBe('Bound');
    expect(policy.premium).toBe(underwritingResult.premium);

    console.log('\n✅ Class B Quote-to-Policy Flow Successful!');
  });

  test('declined quote does not create policy', async ({ request }) => {
    // Step 1: Create Customer
    const customer = await createCustomer(request, {
      firstName: 'Charlie',
      lastName: 'Brown',
    });

    console.log(`✓ Customer created: ${customer.customerId}`);

    // Step 2: Start Quote
    const quote = await startQuote(request, customer.customerId);
    console.log(`✓ Quote started: ${quote.quoteId}`);

    // Step 3: Submit Underwriting with decline criteria
    const underwritingResponse = await submitUnderwriting(request, quote.quoteId, {
      priorClaimsCount: 3, // Decline: >2 claims
      propertyAgeYears: 15,
      creditTier: 'Excellent',
    });
    expect(underwritingResponse.status()).toBe(422);
    const underwritingResult = await underwritingResponse.json();

    console.log(`✓ Underwriting submitted: ${underwritingResult.status}, Reason: ${underwritingResult.declineReason}`);
    expect(underwritingResult.error).toBe("UnderwritingDeclined");
    expect(underwritingResult.message).toContain('prior claims');

    // Step 4: Verify quote is declined
    const declinedQuote = await getQuote(request, quote.quoteId);
    expect(declinedQuote.status).toBe('Declined');

    // Step 5: Verify no policy was created (wait briefly then check)
    await new Promise(resolve => setTimeout(resolve, 2000)); // Wait 2s
    
    const policies = await waitForPolicyCreation(request, customer.customerId, 3000)
      .catch(() => null); // Expect this to timeout

    expect(policies).toBeNull();

    console.log('\n✅ Declined Quote Does Not Create Policy - As Expected!');
  });
});
