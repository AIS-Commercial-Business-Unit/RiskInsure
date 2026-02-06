import { APIRequestContext, expect } from '@playwright/test';
import { getTestConfig } from '../config/api-endpoints';

const config = getTestConfig();

export interface CreateCustomerRequest {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  address: {
    street: string;
    city: string;
    state: string;
    zipCode: string;
  };
}

export interface Customer {
  customerId: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  address: {
    street: string;
    city: string;
    state: string;
    zipCode: string;
  };
  createdUtc: string;
}

/**
 * Create a new customer in the Customer domain
 */
export async function createCustomer(
  request: APIRequestContext,
  customerData?: Partial<CreateCustomerRequest>
): Promise<Customer> {
  const timestamp = Date.now();
  const defaultData: CreateCustomerRequest = {
    firstName: 'John',
    lastName: 'Doe',
    email: `${config.testData.customerPrefix}${timestamp}@example.com`,
    phone: '555-0100',
    address: {
      street: '123 Main St',
      city: 'Beverly Hills',
      state: 'CA',
      zipCode: '90210',
    },
  };

  const data = { ...defaultData, ...customerData };

  const response = await request.post(`${config.apis.customer}/api/customers`, {
    data,
    timeout: config.timeouts.apiRequest,
  });

  expect(response.status()).toBe(201);
  return await response.json();
}

/**
 * Get customer by ID
 */
export async function getCustomer(
  request: APIRequestContext,
  customerId: string
): Promise<Customer> {
  const response = await request.get(
    `${config.apis.customer}/api/customers/${customerId}`,
    { timeout: config.timeouts.apiRequest }
  );

  expect(response.status()).toBe(200);
  return await response.json();
}
