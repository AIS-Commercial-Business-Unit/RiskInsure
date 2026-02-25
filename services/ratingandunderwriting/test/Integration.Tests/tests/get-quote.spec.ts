import { test, expect } from '@playwright/test';

test.describe('Get Quote', () => {
    // NOTE: Test retrieval of quote information at various stages

    let quoteId: string;
    let customerId: string;

    test.beforeEach(async ({ request }) => {
        customerId = crypto.randomUUID();

        // Create a quote for testing
        const response = await request.post('/api/quotes/start', {
            data: {
                customerId,
                structureCoverageLimit: 200000,
                structureDeductible: 1000,
                contentsCoverageLimit: 50000,
                contentsDeductible: 500,
                termMonths: 12,
                effectiveDate: new Date(Date.now() + 86400000).toISOString(),
                propertyZipCode: '60601'
            }
        });

        expect(response.status()).toBe(201);

        const result = await response.json();
        quoteId = result.quoteId;
    });

    test('should retrieve draft quote successfully', async ({ request }) => {
        const response = await request.get(`/api/quotes/${quoteId}`);

        expect(response.status()).toBe(200);

        const quote = await response.json();
        expect(quote.quoteId).toBe(quoteId);
        expect(quote.customerId).toBe(customerId);
        expect(quote.status).toBe('Draft');
        expect(quote.structureCoverageLimit).toBe(200000);
        expect(quote.structureDeductible).toBe(1000);
        expect(quote.contentsCoverageLimit).toBe(50000);
        expect(quote.contentsDeductible).toBe(500);
        expect(quote.termMonths).toBe(12);
        expect(quote.effectiveDate).toBeDefined();
        expect(quote.expirationUtc).toBeDefined();
        expect(quote.createdUtc).toBeDefined();
        expect(quote.premium).toBeNull();
        expect(quote.underwritingClass).toBeNull();
    });

    test('should retrieve quoted quote with premium', async ({ request }) => {
        // Submit underwriting to get premium
        const uwResponse = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
            data: {
                priorClaimsCount: 0,
                propertyAgeYears: 10,
                creditTier: 'Excellent'
            }
        });

        expect(uwResponse.status()).toBe(200);

        const response = await request.get(`/api/quotes/${quoteId}`);

        expect(response.status()).toBe(200);

        const quote = await response.json();
        expect(quote.quoteId).toBe(quoteId);
        expect(quote.status).toBe('Quoted');
        expect(quote.premium).toBeGreaterThan(0);
        expect(quote.underwritingClass).toBe('A');
    });

    test('should retrieve accepted quote', async ({ request }) => {
        // Submit underwriting first
        const uwResponse = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
            data: {
                priorClaimsCount: 0,
                propertyAgeYears: 10,
                creditTier: 'Excellent'
            }
        });

        expect(uwResponse.status()).toBe(200);

        // Accept the quote
        const acceptResponse = await request.post(`/api/quotes/${quoteId}/accept`);

        expect(acceptResponse.status()).toBe(200);

        const response = await request.get(`/api/quotes/${quoteId}`);

        expect(response.status()).toBe(200);

        const quote = await response.json();
        expect(quote.quoteId).toBe(quoteId);
        expect(quote.status).toBe('Accepted');
        expect(quote.premium).toBeGreaterThan(0);
        expect(quote.underwritingClass).toBe('A');
    });

    test('should return 404 for non-existent quote', async ({ request }) => {
        const nonExistentId = 'QUOTE-' + Date.now();

        const response = await request.get(`/api/quotes/${nonExistentId}`);

        expect(response.status()).toBe(404);

        const error = await response.json();
        expect(error.error).toBe('QuoteNotFound');
    });

    test('should retrieve quote with different underwriting classes', async ({ request }) => {
        // Submit Class B underwriting
        const uwResponse = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
            data: {
                priorClaimsCount: 1,
                propertyAgeYears: 25,
                creditTier: 'Good'
            }
        });

        expect(uwResponse.status()).toBe(200);

        const response = await request.get(`/api/quotes/${quoteId}`);

        expect(response.status()).toBe(200);

        const quote = await response.json();
        expect(quote.status).toBe('Quoted');
        expect(quote.underwritingClass).toBe('B');
        expect(quote.premium).toBeGreaterThan(0);
    });
});
