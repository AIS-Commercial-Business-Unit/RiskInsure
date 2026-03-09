import { expect, request as playwrightRequest, test } from '@playwright/test';

const apiVersion = process.env.KEYVAULT_API_VERSION || '7.4';
const vaultUrl = process.env.KEYVAULT_EMULATOR_URL || 'https://localhost:4997';
const authToken =
  process.env.KEYVAULT_EMULATOR_TOKEN ||
  'eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiJyaXNraW5zdXJlLWUyZSIsImlhdCI6MTcwMDAwMDAwMH0.';

test.describe('Key Vault Emulator E2E', () => {
  test('creates a secret and retrieves the same value', async () => {
    const context = await playwrightRequest.newContext({
      baseURL: vaultUrl,
      ignoreHTTPSErrors: true,
      extraHTTPHeaders: {
        Accept: 'application/json',
        Authorization: `Bearer ${authToken}`,
        'Content-Type': 'application/json',
      },
    });

    const secretName = `e2e-secret-${Date.now()}`;
    const secretValue = `value-${Date.now()}-${Math.random().toString(36).slice(2)}`;

    const createResponse = await context.put(`/secrets/${secretName}?api-version=${apiVersion}`, {
      data: {
        value: secretValue,
      },
    });

    expect(createResponse.ok(), await createResponse.text()).toBeTruthy();

    const getResponse = await context.get(`/secrets/${secretName}?api-version=${apiVersion}`);
    expect(getResponse.ok(), await getResponse.text()).toBeTruthy();

    const retrievedSecret = await getResponse.json();
    expect(retrievedSecret.value).toBe(secretValue);

    await context.dispose();
  });
});
