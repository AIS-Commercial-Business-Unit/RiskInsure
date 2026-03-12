import { expect, test } from '@playwright/test';
import { getTestConfig, validateConfig } from '../../config/api-endpoints';

const config = getTestConfig();

function buildRelationshipRequest(suffix: string) {
  return {
    firstName: 'Casey',
    lastName: 'Relationship',
    email: `crm-${suffix}@example.com`,
    phone: '555-0142',
    address: {
      street: '123 Main St',
      city: 'Beverly Hills',
      state: 'CA',
      zipCode: '90210',
    },
    birthDate: '1990-05-15T00:00:00Z',
  };
}

test.describe('[Generated] customerrelationshipsmgt requirements regression', () => {
  test.beforeAll(() => {
    validateConfig(config);
  });

  test('service health endpoint responds for customerrelationshipsmgt', async ({ request }) => {
    const health = await request.get(`${config.apis.customerrelationshipsmgt}/health`);
    expect([200, 204]).toContain(health.status());
  });

  test('relationship can be created, retrieved, updated, and change-email queued', async ({ request }) => {
    const suffix = Date.now().toString();
    const createResponse = await request.post(`${config.apis.customerrelationshipsmgt}/api/relationships`, {
      data: buildRelationshipRequest(suffix),
      timeout: config.timeouts.apiRequest,
    });

    expect(createResponse.status()).toBe(201);
    const created = await createResponse.json();
    expect(created.relationshipId).toMatch(/^CRM-\d+$/);
    expect(created.status).toBe('Active');

    const getResponse = await request.get(`${config.apis.customerrelationshipsmgt}/api/relationships/${created.relationshipId}`);
    expect(getResponse.status()).toBe(200);

    const updateResponse = await request.put(`${config.apis.customerrelationshipsmgt}/api/relationships/${created.relationshipId}`, {
      data: {
        firstName: 'Jordan',
        phoneNumber: '555-0199',
        mailingAddress: {
          street: '500 Sunset Blvd',
          city: 'Los Angeles',
          state: 'CA',
          zipCode: '90001',
        },
      },
    });

    expect(updateResponse.status()).toBe(200);
    const updated = await updateResponse.json();
    expect(updated.firstName).toBe('Jordan');
    expect(updated.phone).toBe('555-0199');

    const changeEmailResponse = await request.post(`${config.apis.customerrelationshipsmgt}/api/relationships/${created.relationshipId}/change-email`, {
      data: { newEmail: `crm-${suffix}-new@example.com` },
    });

    expect(changeEmailResponse.status()).toBe(202);
    const changed = await changeEmailResponse.json();
    expect(changed.relationshipId).toBe(created.relationshipId);
  });

  test('invalid create is rejected and deleted relationship returns 404', async ({ request }) => {
    const invalidResponse = await request.post(`${config.apis.customerrelationshipsmgt}/api/relationships`, {
      data: {
        firstName: 'Invalid',
        lastName: 'Relationship',
        email: 'not-an-email',
        phone: '555-0100',
        address: {
          street: '1 Broken Rd',
          city: 'Nowhere',
          state: 'CA',
          zipCode: '1234',
        },
        birthDate: '2010-01-01T00:00:00Z',
      },
    });

    expect(invalidResponse.status()).toBe(400);
    const invalidBody = await invalidResponse.json();
    expect(invalidBody.error).toBe('ValidationFailed');

    const suffix = `${Date.now()}-delete`;
    const createResponse = await request.post(`${config.apis.customerrelationshipsmgt}/api/relationships`, {
      data: buildRelationshipRequest(suffix),
    });

    expect(createResponse.status()).toBe(201);
    const created = await createResponse.json();

    const deleteResponse = await request.delete(`${config.apis.customerrelationshipsmgt}/api/relationships/${created.relationshipId}`);
    expect(deleteResponse.status()).toBe(204);

    const getAfterDelete = await request.get(`${config.apis.customerrelationshipsmgt}/api/relationships/${created.relationshipId}`);
    expect(getAfterDelete.status()).toBe(404);
  });
});

test.describe('[Generated] metadata for customerrelationshipsmgt', () => {
  test('source documents snapshot', async () => {
    const snapshot = [
      '- services/customerrelationshipsmgt/docs/business/relationship-management.md',
      '- services/customerrelationshipsmgt/docs/technical/relationship-technical-spec.md',
    ];

    expect(snapshot.length).toBeGreaterThan(0);
  });
});
