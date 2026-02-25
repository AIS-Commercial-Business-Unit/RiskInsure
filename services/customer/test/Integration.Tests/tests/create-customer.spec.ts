import { test, expect } from '@playwright/test';

import { randomUUID } from 'node:crypto';

test.describe('Customer API - Create Customer', () => {
  test('should create a new customer successfully', async ({ request }) => {
    const email = `test-${randomUUID()}@example.com`;
    
    const response = await request.post('/api/customers', {
      data: {
        firstName: 'Test',
        lastName: 'User',
        email: email,
        phone: '+1-555-1234',
        address: {
          street: '123 Main St',
          city: 'Beverly Hills',
          state: 'CA',
          zipCode: '90210'
        },
        birthDate: '1990-05-15T00:00:00Z'
      }
    });

    expect(response.status()).toBe(201);
    
    const customer = await response.json();
    expect(customer.customerId).toBeDefined();
    expect(customer.customerId).toContain('CUST-');
    expect(customer.status).toBe('Active');
    expect(customer.emailVerified).toBe(false);
    expect(customer.email).toBe(email);
    expect(customer.zipCode).toBe('90210');
    expect(customer.firstName).toBe('Test');
    expect(customer.lastName).toBe('User');
    expect(customer.phone).toBe('+1-555-1234');
    expect(customer.address).toBeDefined();
    expect(customer.address.zipCode).toBe('90210');
  });

  test('should reject customer with invalid email', async ({ request }) => {
    const email = 'invalid-email';
    
    const response = await request.post('/api/customers', {
      data: {
        firstName: 'Test',
        lastName: 'User',
        email: email,
        phone: '+1-555-1234',
        address: {
          street: '123 Main St',
          city: 'Beverly Hills',
          state: 'CA',
          zipCode: '90210'
        }
      }
    });

    expect(response.status()).toBe(400);
    
    // ASP.NET Core model validation - ValidationProblemDetails format
    const error = await response.json();
    expect(error.status).toBe(400);
    expect(error.errors?.Email).toBeDefined();
    expect(Array.isArray(error.errors.Email)).toBe(true);
  });

  test('should reject customer under 18 years old', async ({ request }) => {
    const email = `test-${randomUUID()}@example.com`;
    const birthDate = new Date();
    birthDate.setFullYear(birthDate.getFullYear() - 17);
    
    const response = await request.post('/api/customers', {
      data: {
        firstName: 'Test',
        lastName: 'User',
        email: email,
        phone: '+1-555-1234',
        address: {
          street: '123 Main St',
          city: 'Beverly Hills',
          state: 'CA',
          zipCode: '90210'
        },
        birthDate: birthDate.toISOString(),
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
    const email = `duplicate-${randomUUID()}@example.com`;
    
    // Create first customer
    await request.post('/api/customers', {
      data: {
        firstName: 'First',
        lastName: 'User',
        email: email,
        phone: '+1-555-0001',
        address: {
          street: '123 Main St',
          city: 'Beverly Hills',
          state: 'CA',
          zipCode: '90210'
        },
        birthDate: '1990-05-15T00:00:00Z'
      }
    });

    // Attempt to create second customer with same email
    const response = await request.post('/api/customers', {
      data: {
        firstName: 'Second',
        lastName: 'User',
        email: email,
        phone: '+1-555-0002',
        address: {
          street: '123 Main St',
          city: 'Beverly Hills',
          state: 'CA',
          zipCode: '90210'
        },
        birthDate: '1990-05-15T00:00:00Z'
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
    const email = `test-${randomUUID()}@example.com`;
    
    const response = await request.post('/api/customers', {
      data: {
        firstName: 'Test',
        lastName: 'User',
        email: email,
        phone: '+1-555-1234',
        address: {
          street: '123 Main St',
          city: 'Beverly Hills',
          state: 'CA',
          zipCode: '1234'  // Invalid - only 4 digits
        },
        birthDate: '1990-05-15T00:00:00Z'
      }
    });

    expect(response.status()).toBe(400);
    
    // Business validation - custom format
    const error = await response.json();
    expect(error.error).toBe('ValidationFailed');
    expect(error.errors.ZipCode).toBeDefined();
    expect(Array.isArray(error.errors.ZipCode)).toBe(true);
  });

  test('should reject missing required fields', async ({ request }) => {
    const response = await request.post('/api/customers', {
      data: {
        email: `test-${randomUUID()}@example.com`
      }
    });

    expect(response.status()).toBe(400);

    const error = await response.json();
    expect(error.status).toBe(400);
    expect(error.errors).toBeDefined();
    expect(error.errors.FirstName).toBeDefined();
    expect(error.errors.LastName).toBeDefined();
    expect(error.errors.Phone).toBeDefined();

    const errorKeys = Object.keys(error.errors);
    const hasAddressError =
      typeof error.errors.Address !== 'undefined' ||
      errorKeys.some(k => k === 'Address' || k.startsWith('Address.'));
    expect(hasAddressError).toBe(true);
  });
});
