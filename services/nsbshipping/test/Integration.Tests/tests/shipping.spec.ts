import { test, expect } from '@playwright/test';
import crypto from 'crypto';

// NOTE: Tests requiring cross-domain data deferred to enterprise integration tests

test('should return 404 when shipment not found', async ({ request }) => {
  const nonExistentId = crypto.randomUUID();
  const response = await request.get(`/api/shipping/${nonExistentId}`);
  expect(response.status()).toBe(404);
});
