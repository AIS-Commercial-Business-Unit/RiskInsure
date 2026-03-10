import { test, expect } from '@playwright/test';

import { randomUUID } from 'node:crypto';

test.describe('CustomerRelationshipsMgt API - Get and Update Relationship', () => {
  let relationshipId: string;

  test.beforeAll(async ({ request }) => {
    // Create a relationship for testing
    const email = `test-${randomUUID()}@example.com`;

    const createResponse = await request.post('/api/relationships', {
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

    expect(createResponse.status()).toBe(201);
    const created = await createResponse.json();
    relationshipId = created.relationshipId;
    expect(relationshipId).toBeDefined();
  });

  test('should retrieve relationship by ID', async ({ request }) => {
    const response = await request.get(`/api/relationships/${relationshipId}`);

    expect(response.status()).toBe(200);
    
    const relationship = await response.json();
    expect(relationship.relationshipId).toBe(relationshipId);
    expect(relationship.email).toContain('@example.com');
    expect(relationship.zipCode).toBe('90210');
  });

  test('should return 404 for non-existent relationship', async ({ request }) => {
    const nonExistentId = randomUUID();
    
    const response = await request.get(`/api/relationships/${nonExistentId}`);

    expect(response.status()).toBe(404);
  });

  test('should update relationship information', async ({ request }) => {
    const response = await request.put(`/api/relationships/${relationshipId}`, {
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
    
    const relationship = await response.json();
    expect(relationship.firstName).toBe('John');
    expect(relationship.lastName).toBe('Doe');
    expect(relationship.phone).toBe('+1-555-1234');
    expect(relationship.address).toBeDefined();
    expect(relationship.address.city).toBe('Beverly Hills');
  });

  test('should handle partial updates', async ({ request }) => {
    const response = await request.put(`/api/relationships/${relationshipId}`, {
      data: {
        firstName: 'Jane'
      }
    });

    expect(response.status()).toBe(200);
    
    const relationship = await response.json();
    expect(relationship.firstName).toBe('Jane');
    // Previous values should be preserved
    expect(relationship.lastName).toBe('Doe');
  });
});
