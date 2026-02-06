import { test, expect } from '@playwright/test';

test.describe('Customer API - Get and Update Customer', () => {
  let customerId: string;

  test.beforeAll(async ({ request }) => {
    // Create a customer for testing
    customerId = crypto.randomUUID();
    
    await request.post('/api/customers', {
      data: {
        customerId: customerId,
        email: `test-${customerId}@example.com`,
        birthDate: '1990-05-15T00:00:00Z',
        zipCode: '90210'
      }
    });
  });

  test('should retrieve customer by ID', async ({ request }) => {
    const response = await request.get(`/api/customers/${customerId}`);

    expect(response.status()).toBe(200);
    
    const customer = await response.json();
    expect(customer.customerId).toBe(customerId);
    expect(customer.email).toContain('@example.com');
    expect(customer.zipCode).toBe('90210');
  });

  test('should return 404 for non-existent customer', async ({ request }) => {
    const nonExistentId = crypto.randomUUID();
    
    const response = await request.get(`/api/customers/${nonExistentId}`);

    expect(response.status()).toBe(404);
  });

  test('should update customer information', async ({ request }) => {
    const response = await request.put(`/api/customers/${customerId}`, {
      data: {
        firstName: 'John',
        lastName: 'Doe',
        phoneNumber: '+1-555-1234',
        mailingAddress: {
          street: '123 Main St',
          city: 'Beverly Hills',
          state: 'CA',
          zipCode: '90210'
        }
      }
    });

    expect(response.status()).toBe(200);
    
    const customer = await response.json();
    expect(customer.firstName).toBe('John');
    expect(customer.lastName).toBe('Doe');
    expect(customer.phoneNumber).toBe('+1-555-1234');
    expect(customer.mailingAddress).toBeDefined();
    expect(customer.mailingAddress.city).toBe('Beverly Hills');
  });

  test('should handle partial updates', async ({ request }) => {
    const response = await request.put(`/api/customers/${customerId}`, {
      data: {
        firstName: 'Jane'
      }
    });

    expect(response.status()).toBe(200);
    
    const customer = await response.json();
    expect(customer.firstName).toBe('Jane');
    // Previous values should be preserved
    expect(customer.lastName).toBe('Doe');
  });
});
