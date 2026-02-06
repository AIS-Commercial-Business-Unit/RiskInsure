import { test, expect } from '@playwright/test';

test.describe('Customer API - Create Customer', () => {
  test('should create a new customer successfully', async ({ request }) => {
    const customerId = crypto.randomUUID();
    
    const response = await request.post('/api/customers', {
      data: {
        customerId: customerId,
        email: `test-${customerId}@example.com`,
        birthDate: '1990-05-15T00:00:00Z',
        zipCode: '90210'
      }
    });

    expect(response.status()).toBe(201);
    
    const customer = await response.json();
    expect(customer.customerId).toBe(customerId);
    expect(customer.status).toBe('Active');
    expect(customer.emailVerified).toBe(false);
  });

  test('should reject customer with invalid email', async ({ request }) => {
    const customerId = crypto.randomUUID();
    
    const response = await request.post('/api/customers', {
      data: {
        customerId: customerId,
        email: 'invalid-email',
        birthDate: '1990-05-15T00:00:00Z',
        zipCode: '90210'
      }
    });

    expect(response.status()).toBe(400);
    
    // ASP.NET Core model validation - ProblemDetails format
    const error = await response.json();
    expect(error.status).toBe(400);
    expect(error.errors.Email).toBeDefined();
    expect(Array.isArray(error.errors.Email)).toBe(true);
  });

  test('should reject customer under 18 years old', async ({ request }) => {
    const customerId = crypto.randomUUID();
    const birthDate = new Date();
    birthDate.setFullYear(birthDate.getFullYear() - 17);
    
    const response = await request.post('/api/customers', {
      data: {
        customerId: customerId,
        email: `test-${customerId}@example.com`,
        birthDate: birthDate.toISOString(),
        zipCode: '90210'
      }
    });

    expect(response.status()).toBe(400);
    
    // Business validation - custom format
    const error = await response.json();
    expect(error.error).toBe('ValidationFailed');
    expect(error.errors.BirthDate).toBeDefined();
    expect(Array.isArray(error.errors.BirthDate)).toBe(true);
  });

  test('should reject duplicate email', async ({ request }) => {
    const email = `duplicate-${crypto.randomUUID()}@example.com`;
    
    // Create first customer
    await request.post('/api/customers', {
      data: {
        customerId: crypto.randomUUID(),
        email: email,
        birthDate: '1990-05-15T00:00:00Z',
        zipCode: '90210'
      }
    });

    // Attempt to create second customer with same email
    const response = await request.post('/api/customers', {
      data: {
        customerId: crypto.randomUUID(),
        email: email,
        birthDate: '1990-05-15T00:00:00Z',
        zipCode: '90210'
      }
    });

    expect(response.status()).toBe(400);
    
    // Business validation - custom format
    const error = await response.json();
    expect(error.error).toBe('ValidationFailed');
    expect(error.errors.Email).toBeDefined();
    expect(Array.isArray(error.errors.Email)).toBe(true);
    expect(error.errors.Email[0]).toContain('A customer with this email address already exists');
  });

  test('should reject invalid zip code', async ({ request }) => {
    const customerId = crypto.randomUUID();
    
    const response = await request.post('/api/customers', {
      data: {
        customerId: customerId,
        email: `test-${customerId}@example.com`,
        birthDate: '1990-05-15T00:00:00Z',
        zipCode: '1234'  // Invalid - only 4 digits
      }
    });

    expect(response.status()).toBe(400);
    
    // Business validation - custom format
    const error = await response.json();
    expect(error.error).toBe('ValidationFailed');
    expect(error.errors.ZipCode).toBeDefined();
    expect(Array.isArray(error.errors.ZipCode)).toBe(true);
  });
});
