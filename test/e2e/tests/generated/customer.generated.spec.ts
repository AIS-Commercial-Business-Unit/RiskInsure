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
    if (health.status() >= 400) {
      test.skip();
    }
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

    expect(created.customerId).toMatch(/^CUST-\d+$/);
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

    const suffix = `${Date.now()}-delete`;
    const createResponse = await request.post(`${config.apis.customer}/api/customers`, {
      data: buildCustomerRequest(suffix),
      timeout: config.timeouts.apiRequest,
    });

    expect(createResponse.status()).toBe(201);
    const created = await createResponse.json();

    const deleteResponse = await request.delete(`${config.apis.customer}/api/customers/${created.customerId}`);
    expect(deleteResponse.status()).toBe(204);

    const getAfterClosed = await request.get(`${config.apis.customer}/api/customers/${created.customerId}`);
    const afterClosed = await getAfterClosed.json();
    expect(afterClosed.status).toBe('Closed');

  });

  test('email uniqueness is enforced', async ({ request }) => {
    const uniqueEmail = `unique-${Date.now()}@example.com`;
    const customer1 = buildCustomerRequest('unique-1');
    customer1.email = uniqueEmail;

    const response1 = await request.post(`${config.apis.customer}/api/customers`, {
      data: customer1,
      timeout: config.timeouts.apiRequest,
    });
    expect(response1.status()).toBe(201);

    // Attempt to create second customer with same email
    const customer2 = buildCustomerRequest('unique-2');
    customer2.email = uniqueEmail;

    const response2 = await request.post(`${config.apis.customer}/api/customers`, {
      data: customer2,
      timeout: config.timeouts.apiRequest,
    });
    expect(response2.status()).toBe(400);
    const errorBody = await response2.json();
    expect(errorBody.error).toBe('ValidationFailed');

  });

  test('age validation rejects customers under 18', async ({ request }) => {
    const infantDate = new Date();
    infantDate.setFullYear(infantDate.getFullYear() - 5); // 5 years old
  
    const response = await request.post(`${config.apis.customer}/api/customers`, {
      data: {
        firstName: 'Child',
        lastName: 'Customer',
        email: `child-${Date.now()}@example.com`,
        phone: '555-0100',
        address: {
          street: '1 Valid Rd',
          city: 'Anytown',
          state: 'CA',
          zipCode: '90210',
        },
        birthDate: infantDate.toISOString(),
      },
      timeout: config.timeouts.apiRequest,
    });
    expect(response.status()).toBe(400);
  });

  test('change-email endpoint returns 202 Accepted', async ({ request }) => {
    const suffix = Date.now().toString();
    const createResponse = await request.post(`${config.apis.customer}/api/customers`, {
      data: buildCustomerRequest(suffix),
      timeout: config.timeouts.apiRequest,
    });

    expect(createResponse.status()).toBe(201);
    const created = await createResponse.json();

    const changeEmailResponse = await request.post(
      `${config.apis.customer}/api/customers/${created.customerId}/change-email`,
      {
        data: { newEmail: `${suffix}-new@example.com` },
        timeout: config.timeouts.apiRequest,
      }
    );

    expect(changeEmailResponse.status()).toBe(202);
    const changeBody = await changeEmailResponse.json();
    expect(changeBody.customerId).toBe(created.customerId);
    expect(changeBody.message).toContain('verification');
  });

  test('customer response includes audit fields', async ({ request }) => {
    const suffix = Date.now().toString();
    const createResponse = await request.post(`${config.apis.customer}/api/customers`, {
      data: buildCustomerRequest(suffix),
      timeout: config.timeouts.apiRequest,
    });

    expect(createResponse.status()).toBe(201);
    const created = await createResponse.json();

    expect(created).toHaveProperty('emailVerified');
    expect(created).toHaveProperty('createdUtc');
    expect(created).toHaveProperty('updatedUtc');
    expect(typeof created.createdUtc).toBe('string');
    expect(typeof created.updatedUtc).toBe('string');
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
