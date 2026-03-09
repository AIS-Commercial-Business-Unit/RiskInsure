import { test, expect } from '@playwright/test';

import { randomUUID } from 'node:crypto';

test.describe('CustomerRelationshipsMgt API - Create Relationship', () => {
  test('should create a new relationship successfully', async ({ request }) => {
    const email = `test-${randomUUID()}@example.com`;
    
    const response = await request.post('/api/relationships', {
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
    
    const relationship = await response.json();
    expect(relationship.relationshipId).toBeDefined();
    expect(relationship.relationshipId).toContain('CRM-');
    expect(relationship.status).toBe('Active');
    expect(relationship.emailVerified).toBe(false);
    expect(relationship.email).toBe(email);
    expect(relationship.zipCode).toBe('90210');
    expect(relationship.firstName).toBe('Test');
    expect(relationship.lastName).toBe('User');
    expect(relationship.phone).toBe('+1-555-1234');
    expect(relationship.address).toBeDefined();
    expect(relationship.address.zipCode).toBe('90210');
  });

  test('should reject relationship with invalid email', async ({ request }) => {
    const email = 'invalid-email';
    
    const response = await request.post('/api/relationships', {
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

  test('should reject individual under 18 years old', async ({ request }) => {
    const email = `test-${randomUUID()}@example.com`;
    const birthDate = new Date();
    birthDate.setFullYear(birthDate.getFullYear() - 17);
    
    const response = await request.post('/api/relationships', {
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
    
    // Create first relationship
    await request.post('/api/relationships', {
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

    // Attempt to create second relationship with same email
    const response = await request.post('/api/relationships', {
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
    expect(error.errors.Email[0]).toContain('A relationship with this email address already exists');
  });

  test('should reject invalid zip code', async ({ request }) => {
    const email = `test-${randomUUID()}@example.com`;
    
    const response = await request.post('/api/relationships', {
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
    const response = await request.post('/api/relationships', {
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
