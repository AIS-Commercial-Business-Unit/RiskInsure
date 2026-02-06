import { test, expect } from '@playwright/test';

test.describe('Customer API - Delete Customer (GDPR)', () => {
  test('should close customer account and anonymize data', async ({ request }) => {
    // Create a customer
    const customerId = crypto.randomUUID();
    
    await request.post('/api/customers', {
      data: {
        customerId: customerId,
        email: `test-${customerId}@example.com`,
        birthDate: '1990-05-15T00:00:00Z',
        zipCode: '90210'
      }
    });

    // Update with personal information
    await request.put(`/api/customers/${customerId}`, {
      data: {
        firstName: 'John',
        lastName: 'Doe',
        phoneNumber: '+1-555-1234'
      }
    });

    // Delete the customer
    const deleteResponse = await request.delete(`/api/customers/${customerId}`);
    expect(deleteResponse.status()).toBe(204);

    // Verify customer is closed and anonymized
    const getResponse = await request.get(`/api/customers/${customerId}`);
    expect(getResponse.status()).toBe(200);
    
    const customer = await getResponse.json();
    expect(customer.status).toBe('Closed');
    expect(customer.email).toContain('anonymized');
    expect(customer.firstName).toBeNull();
    expect(customer.lastName).toBeNull();
    expect(customer.phoneNumber).toBeNull();
  });

  test('should return 404 when deleting non-existent customer', async ({ request }) => {
    const nonExistentId = crypto.randomUUID();
    
    const response = await request.delete(`/api/customers/${nonExistentId}`);

    expect(response.status()).toBe(404);
  });
});

test.describe('Customer API - Email Change', () => {
  test('should accept email change request', async ({ request }) => {
    // Create a customer
    const customerId = crypto.randomUUID();
    
    await request.post('/api/customers', {
      data: {
        customerId: customerId,
        email: `test-${customerId}@example.com`,
        birthDate: '1990-05-15T00:00:00Z',
        zipCode: '90210'
      }
    });

    // Request email change
    const response = await request.post(`/api/customers/${customerId}/change-email`, {
      data: {
        newEmail: `new-${customerId}@example.com`
      }
    });

    expect(response.status()).toBe(202);
    
    const result = await response.json();
    expect(result.message).toContain('verification');
  });
});
