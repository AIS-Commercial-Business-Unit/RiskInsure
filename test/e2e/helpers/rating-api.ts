import { APIRequestContext, expect } from '@playwright/test';
import { getTestConfig } from '../config/api-endpoints';

const config = getTestConfig();

export interface StartQuoteRequest {
  customerId: string;
  structureCoverageLimit: number;
  structureDeductible: number;
  contentsCoverageLimit?: number;
  contentsDeductible?: number;
  termMonths: number;
  effectiveDate: string;
  propertyZipCode: string;
}

export interface SubmitUnderwritingRequest {
  priorClaimsCount: number;
  propertyAgeYears: number;
  creditTier: string;
}

export interface Quote {
  quoteId: string;
  customerId: string;
  status: string;
  premium?: number;
  underwritingClass?: string;
  expirationUtc?: string;
  structureCoverageLimit: number;
  structureDeductible: number;
  contentsCoverageLimit?: number;
  contentsDeductible?: number;
  termMonths: number;
  effectiveDate: string;
  createdUtc: string;
}

export interface UnderwritingResponse {
  quoteId: string;
  underwritingClass?: string;
  premium?: number;
  status: string;
  expirationUtc?: string;
  declineReason?: string;
}

export interface AcceptQuoteResponse {
  quoteId: string;
  status: string;
  policyCreationInitiated: boolean;
  acceptedUtc: string;
}

/**
 * Start a new quote in the Rating & Underwriting domain
 */
export async function startQuote(
  request: APIRequestContext,
  customerId: string,
  quoteData?: Partial<Omit<StartQuoteRequest, 'customerId'>>
): Promise<Quote> {
  const defaultData: StartQuoteRequest = {
    customerId,
    structureCoverageLimit: 300000,
    structureDeductible: 1000,
    contentsCoverageLimit: 100000,
    contentsDeductible: 500,
    termMonths: 12,
    effectiveDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(), // 30 days from now
    propertyZipCode: '90210',
  };

  const data = { ...defaultData, ...quoteData };

  const response = await request.post(`${config.apis.rating}/api/quotes/start`, {
    data,
    timeout: config.timeouts.apiRequest,
  });

  expect(response.status()).toBe(200);
  return await response.json();
}

/**
 * Submit underwriting for a quote
 */
export async function submitUnderwriting(
  request: APIRequestContext,
  quoteId: string,
  underwritingData?: Partial<SubmitUnderwritingRequest>
): Promise<UnderwritingResponse> {
  const defaultData: SubmitUnderwritingRequest = {
    priorClaimsCount: 0,
    propertyAgeYears: 10,
    creditTier: 'Excellent',
  };

  const data = { ...defaultData, ...underwritingData };

  const response = await request.post(
    `${config.apis.rating}/api/quotes/${quoteId}/submit-underwriting`,
    {
      data,
      timeout: config.timeouts.apiRequest,
    }
  );

  expect(response.status()).toBe(200);
  return await response.json();
}

/**
 * Accept a quote (triggers policy creation)
 */
export async function acceptQuote(
  request: APIRequestContext,
  quoteId: string
): Promise<AcceptQuoteResponse> {
  const response = await request.post(
    `${config.apis.rating}/api/quotes/${quoteId}/accept`,
    { timeout: config.timeouts.apiRequest }
  );

  expect(response.status()).toBe(200);
  return await response.json();
}

/**
 * Get quote by ID
 */
export async function getQuote(
  request: APIRequestContext,
  quoteId: string
): Promise<Quote> {
  const response = await request.get(
    `${config.apis.rating}/api/quotes/${quoteId}`,
    { timeout: config.timeouts.apiRequest }
  );

  expect(response.status()).toBe(200);
  return await response.json();
}
