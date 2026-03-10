import { test, expect } from '@playwright/test';

import { randomUUID } from 'node:crypto';

test.describe('CustomerRelationshipsMgt API - Close Relationship (GDPR)', () => {
  test('should close relationship account and anonymize data', async ({ request }) => {
    // Create a relationship
    const email = `test-${randomUUID()}@example.com`;

    const createResponse = await request.post('/api/relationships', {
      data: {
        firstName: 'John',
        lastName: 'Doe',
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
    const relationshipId = created.relationshipId;
    expect(relationshipId).toBeDefined();

    // Update with personal information
    await request.put(`/api/relationships/${relationshipId}`, {
      data: {
        firstName: 'John',
        lastName: 'Doe',
        phoneNumber: '+1-555-1234'
      }
    });

    // Delete the relationship
    const deleteResponse = await request.delete(`/api/relationships/${relationshipId}`);
    expect(deleteResponse.status()).toBe(204);

    // Verify relationship is closed and anonymized
    const getResponse = await request.get(`/api/relationships/${relationshipId}`);
    expect(getResponse.status()).toBe(200);
    
    const relationship = await getResponse.json();
    expect(relationship.status).toBe('Closed');
    expect(relationship.email).toContain('anonymized');
    expect(relationship.firstName).toBeNull();
    expect(relationship.lastName).toBeNull();
    expect(relationship.phone).toBeNull();
  });

  test('should return 404 when deleting non-existent relationship', async ({ request }) => {
    const nonExistentId = randomUUID();
    
    const response = await request.delete(`/api/relationships/${nonExistentId}`);

    expect(response.status()).toBe(404);
  });
});

test.describe('CustomerRelationshipsMgt API - Email Change', () => {
  test('should accept email change request', async ({ request }) => {
    // Create a relationship
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
    const relationshipId = created.relationshipId;
    expect(relationshipId).toBeDefined();

    // Request email change
    const response = await request.post(`/api/relationships/${relationshipId}/change-email`, {
      data: {
        newEmail: `new-${randomUUID()}@example.com`
      }
    });

    expect(response.status()).toBe(202);
    
    const result = await response.json();
    expect(result.message).toContain('verification');
  });
});
