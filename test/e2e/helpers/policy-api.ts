import { APIRequestContext, expect } from '@playwright/test';
import { getTestConfig } from '../config/api-endpoints';

const config = getTestConfig();

export interface Policy {
  policyId: string;
  policyNumber: string;
  customerId: string;
  status: string;
  premium: number;
  structureCoverageLimit: number;
  structureDeductible: number;
  contentsCoverageLimit?: number;
  contentsDeductible?: number;
  termMonths: number;
  effectiveDate: string;
  expirationDate: string;
  createdUtc: string;
}

export interface CustomerPoliciesResponse {
  customerId: string;
  policies: Policy[];
}

/**
 * Get policy by ID
 */
export async function getPolicy(
  request: APIRequestContext,
  policyId: string
): Promise<Policy> {
  const response = await request.get(
    `${config.apis.policy}/api/policies/${policyId}`,
    { timeout: config.timeouts.apiRequest }
  );

  expect(response.status()).toBe(200);
  return await response.json();
}

/**
 * Get all policies for a customer
 */
export async function getCustomerPolicies(
  request: APIRequestContext,
  customerId: string
): Promise<Policy[]> {
  const response = await request.get(
    `${config.apis.policy}/api/customers/${customerId}/policies`,
    { timeout: config.timeouts.apiRequest }
  );

  if (response.status() === 404) {
    return []; // No policies yet
  }

  expect(response.status()).toBe(200);
  const data: CustomerPoliciesResponse = await response.json();
  return data.policies;
}

/**
 * Wait for policy to be created (handles eventual consistency)
 * 
 * Polls the Policy API until a policy is found for the customer.
 * This handles the async nature of QuoteAccepted → PolicyCreated event flow.
 * 
 * @param request - Playwright APIRequestContext
 * @param customerId - Customer ID to check for policies
 * @param timeoutMs - Maximum time to wait (default from config)
 * @param pollIntervalMs - Time between polling attempts (default 500ms)
 * @returns The created policy
 * @throws Error if policy not created within timeout
 */
export async function waitForPolicyCreation(
  request: APIRequestContext,
  customerId: string,
  timeoutMs: number = config.timeouts.eventualConsistency,
  pollIntervalMs: number = 500
): Promise<Policy> {
  const startTime = Date.now();
  let lastError: string | undefined;

  while (Date.now() - startTime < timeoutMs) {
    try {
      const policies = await getCustomerPolicies(request, customerId);
      
      if (policies.length > 0) {
        // Return the most recently created policy
        const sortedPolicies = policies.sort(
          (a, b) => new Date(b.createdUtc).getTime() - new Date(a.createdUtc).getTime()
        );
        return sortedPolicies[0];
      }
    } catch (error) {
      lastError = error instanceof Error ? error.message : String(error);
      // Continue polling on errors
    }

    // Wait before next poll
    await new Promise(resolve => setTimeout(resolve, pollIntervalMs));
  }

  const elapsed = Date.now() - startTime;
  throw new Error(
    `Policy not created within ${timeoutMs}ms for customer ${customerId}. ` +
    `Elapsed: ${elapsed}ms. Last error: ${lastError || 'none'}`
  );
}
