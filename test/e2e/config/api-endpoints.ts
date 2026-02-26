/**
 * API Endpoint Configuration
 * 
 * Centralizes all API base URLs for easy configuration in different environments.
 * Reads from environment variables with sensible defaults for local development.
 */

export interface ApiEndpoints {
  customer: string;
  ratingandunderwriting: string;
  policy: string;
  billing: string;
  fundsTransfer: string;
}

export interface TestConfig {
  apis: ApiEndpoints;
  timeouts: {
    eventualConsistency: number;
    apiRequest: number;
  };
  testData: {
    customerPrefix: string;
    quotePrefix: string;
  };
}

/**
 * Get API configuration from environment variables or defaults
 */
export function getTestConfig(): TestConfig {
  return {
    apis: {
      customer: process.env.CUSTOMER_API_URL || 'http://127.0.0.1:7073',
      // rating: process.env.RATING_API_URL || 'http://127.0.0.1:7079',
      ratingandunderwriting: process.env.RATINGANDUNDERWRITING_API_URL || 'http://127.0.0.1:7079',
      policy: process.env.POLICY_API_URL || 'http://127.0.0.1:7077',
      billing: process.env.BILLING_API_URL || 'http://127.0.0.1:7071',
      fundsTransfer: process.env.FUNDS_TRANSFER_API_URL || 'http://127.0.0.1:7075',
    },
    timeouts: {
      eventualConsistency: parseInt(process.env.EVENTUAL_CONSISTENCY_TIMEOUT || '10000', 10),
      apiRequest: parseInt(process.env.API_REQUEST_TIMEOUT || '30000', 10),
    },
    testData: {
      customerPrefix: process.env.TEST_CUSTOMER_PREFIX || 'E2E-TEST-CUST-',
      quotePrefix: process.env.TEST_QUOTE_PREFIX || 'E2E-TEST-QUOTE-',
    },
  };
}

/**
 * Validate that all required APIs are configured
 */
export function validateConfig(config: TestConfig): void {
  const missingApis: string[] = [];
  
  Object.entries(config.apis).forEach(([name, url]) => {
    if (!url) {
      missingApis.push(name);
    }
  });
  
  if (missingApis.length > 0) {
    throw new Error(
      `Missing API configuration for: ${missingApis.join(', ')}\n` +
      `Please set environment variables or check .env file.`
    );
  }
}
