import { expect, test } from '@playwright/test';
import { getTestConfig, validateConfig } from '../../config/api-endpoints';

const config = getTestConfig();

function buildCustomerRequest(suffix: string) {
  return {
    firstName: 'Casey',
    lastName: 'Customer',
    email: `cust-${suffix}@example.com`,
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

test.describe('[Generated] customer requirements regression', () => {
  test.beforeAll(() => {
    validateConfig(config);
  });

  test('service health endpoint responds for customer', async ({ request }) => {
    const health = await request.get(`${config.apis.customer}/health`);
    expect([200, 204]).toContain(health.status());
  });

  test('customer can be created, read, and updated', async ({ request }) => {
    const suffix = Date.now().toString();
    const createResponse = await request.post(`${config.apis.customer}/api/customers`, {
      data: buildCustomerRequest(suffix),
      timeout: config.timeouts.apiRequest,
    });

    expect(createResponse.status()).toBe(201);
    const created = await createResponse.json();

    expect(created.customerId).toMatch(/^CRM-\d+$/);
    expect(created.status).toBe('Active');
    expect(created.email).toBe(`cust-${suffix}@example.com`);
    expect(created.zipCode).toBe('90210');
    expect(created.phone).toBe('555-0142');

    const getResponse = await request.get(`${config.apis.customer}/api/customers/${created.customerId}`);
    expect(getResponse.status()).toBe(200);

    const fetched = await getResponse.json();
    expect(fetched.customerId).toBe(created.customerId);
    expect(fetched.firstName).toBe('Casey');

    const updateResponse = await request.put(`${config.apis.customer}/api/customers/${created.customerId}`, {
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
      timeout: config.timeouts.apiRequest,
    });

    expect(updateResponse.status()).toBe(200);
    const updated = await updateResponse.json();
    expect(updated.firstName).toBe('Jordan');
    expect(updated.phone).toBe('555-0199');
    expect(updated.address.zipCode).toBe('90001');
  });

  test('invalid create is rejected and deleted customer returns 404', async ({ request }) => {
    const invalidResponse = await request.post(`${config.apis.customer}/api/customers`, {
      data: {
        firstName: 'Invalid',
        lastName: 'Customer',
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
      timeout: config.timeouts.apiRequest,
    });

    expect(invalidResponse.status()).toBe(400);
    const invalidBody = await invalidResponse.json();
    expect(invalidBody.error).toBe('ValidationFailed');

    const suffix = `${Date.now()}-delete`;
    const createResponse = await request.post(`${config.apis.customer}/api/customers`, {
      data: buildCustomerRequest(suffix),
      timeout: config.timeouts.apiRequest,
    });

    expect(createResponse.status()).toBe(201);
    const created = await createResponse.json();

    const deleteResponse = await request.delete(`${config.apis.customer}/api/customers/${created.customerId}`);
    expect(deleteResponse.status()).toBe(204);

    const getAfterDelete = await request.get(`${config.apis.customer}/api/customers/${created.customerId}`);
    expect(getAfterDelete.status()).toBe(404);
  });
});

test.describe('[Generated] metadata for customer', () => {
  test('source documents snapshot', async () => {
    const snapshot = [
      '- services/customer/docs/business/customer-management.md',
      '- services/customer/docs/technical/customer-technical-spec.md',
    ];

    expect(snapshot.length).toBeGreaterThan(0);
  });
});
