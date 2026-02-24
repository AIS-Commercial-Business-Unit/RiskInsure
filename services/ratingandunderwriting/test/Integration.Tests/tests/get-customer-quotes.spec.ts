import { test, expect } from '@playwright/test';

test.describe('Get Customer Quotes', () => {
    // NOTE: Test retrieval of all quotes for a customer

    let customerId: string;
    let quoteIds: string[] = [];

    test.beforeEach(async ({ request }) => {
        customerId = crypto.randomUUID();
        quoteIds = [];
    });

    test('should return empty list for customer with no quotes', async ({ request }) => {
        const response = await request.get(`/api/customers/${customerId}/quotes`);

        expect(response.status()).toBe(200);

        const result = await response.json();
        expect(result.customerId).toBe(customerId);
        expect(result.quotes).toEqual([]);
    });

    test('should return all quotes for customer', async ({ request }) => {
        test.setTimeout(60000); // Extended timeout for multiple API calls
        
        // Create multiple quotes for the same customer
        for (let i = 0; i < 3; i++) {
            const response = await request.post('/api/quotes/start', {
                data: {
                    customerId,
                    structureCoverageLimit: 200000,
                    structureDeductible: 1000,
                    contentsCoverageLimit: 50000,
                    contentsDeductible: 500,
                    termMonths: 12,
                    effectiveDate: new Date(Date.now() + 86400000 * (i + 1)).toISOString(),
                    propertyZipCode: '60601'
                }
            });

            const result = await response.json();
            quoteIds.push(result.quoteId);
        }

        const response = await request.get(`/api/customers/${customerId}/quotes`);

        expect(response.status()).toBe(200);

        const result = await response.json();
        expect(result.customerId).toBe(customerId);
        expect(result.quotes).toHaveLength(3);

        // Verify all quotes are present
        const returnedQuoteIds = result.quotes.map((q: any) => q.quoteId);
        for (const quoteId of quoteIds) {
            expect(returnedQuoteIds).toContain(quoteId);
        }
    });

    test('should return quote summaries with correct fields', async ({ request }) => {
        // Create a quote
        const startResponse = await request.post('/api/quotes/start', {
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

        const startResult = await startResponse.json();
        const quoteId = startResult.quoteId;

        const response = await request.get(`/api/customers/${customerId}/quotes`);

        expect(response.status()).toBe(200);

        const result = await response.json();
        expect(result.quotes).toHaveLength(1);

        const quote = result.quotes[0];
        expect(quote.quoteId).toBe(quoteId);
        expect(quote.status).toBe('Draft');
        expect(quote.premium).toBeNull();
        expect(quote.expirationUtc).toBeDefined();
        expect(quote.createdUtc).toBeDefined();
    });

    test('should include premium in quoted quotes', async ({ request }) => {
        test.setTimeout(60000); // Extended timeout for multiple API calls
        
        // Create and underwrite a quote
        const startResponse = await request.post('/api/quotes/start', {
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

        const startResult = await startResponse.json();
        const quoteId = startResult.quoteId;

        // Submit underwriting
        await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
            data: {
                priorClaimsCount: 0,
                propertyAgeYears: 10,
                creditTier: 'Excellent'
            }
        });

        const response = await request.get(`/api/customers/${customerId}/quotes`);

        expect(response.status()).toBe(200);

        const result = await response.json();
        expect(result.quotes).toHaveLength(1);

        const quote = result.quotes[0];
        expect(quote.quoteId).toBe(quoteId);
        expect(quote.status).toBe('Quoted');
        expect(quote.premium).toBeGreaterThan(0);
    });

    test('should return quotes in different statuses', async ({ request }) => {
        test.setTimeout(90000); // Extended timeout for creating 3 quotes with different statuses
        
        // Create draft quote
        const draftResponse = await request.post('/api/quotes/start', {
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
        const draftResult = await draftResponse.json();

        // Create and quote another
        const quotedResponse = await request.post('/api/quotes/start', {
            data: {
                customerId,
                structureCoverageLimit: 250000,
                structureDeductible: 2000,
                contentsCoverageLimit: 75000,
                contentsDeductible: 1000,
                termMonths: 12,
                effectiveDate: new Date(Date.now() + 86400000).toISOString(),
                propertyZipCode: '60601'
            }
        });
        const quotedResult = await quotedResponse.json();

        await request.post(`/api/quotes/${quotedResult.quoteId}/submit-underwriting`, {
            data: {
                priorClaimsCount: 0,
                propertyAgeYears: 10,
                creditTier: 'Excellent'
            }
        });

        // Create, quote, and accept another
        const acceptedResponse = await request.post('/api/quotes/start', {
            data: {
                customerId,
                structureCoverageLimit: 300000,
                structureDeductible: 2500,
                contentsCoverageLimit: 100000,
                contentsDeductible: 1500,
                termMonths: 12,
                effectiveDate: new Date(Date.now() + 86400000).toISOString(),
                propertyZipCode: '60601'
            }
        });
        const acceptedResult = await acceptedResponse.json();

        await request.post(`/api/quotes/${acceptedResult.quoteId}/submit-underwriting`, {
            data: {
                priorClaimsCount: 0,
                propertyAgeYears: 10,
                creditTier: 'Excellent'
            }
        });
        await request.post(`/api/quotes/${acceptedResult.quoteId}/accept`);

        const response = await request.get(`/api/customers/${customerId}/quotes`);

        expect(response.status()).toBe(200);

        const result = await response.json();
        expect(result.quotes).toHaveLength(3);

        const statuses = result.quotes.map((q: any) => q.status).sort();
        expect(statuses).toEqual(['Accepted', 'Draft', 'Quoted']);
    });

    test('should not return quotes from other customers', async ({ request }) => {
        test.setTimeout(60000); // Extended timeout for multiple API calls
        
        // Create quote for this customer
        const response1 = await request.post('/api/quotes/start', {
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

        // Create quote for different customer
        const otherCustomerId = crypto.randomUUID();
        await request.post('/api/quotes/start', {
            data: {
                customerId: otherCustomerId,
                structureCoverageLimit: 200000,
                structureDeductible: 1000,
                contentsCoverageLimit: 50000,
                contentsDeductible: 500,
                termMonths: 12,
                effectiveDate: new Date(Date.now() + 86400000).toISOString(),
                propertyZipCode: '60601'
            }
        });

        const response = await request.get(`/api/customers/${customerId}/quotes`);

        expect(response.status()).toBe(200);

        const result = await response.json();
        expect(result.customerId).toBe(customerId);
        expect(result.quotes).toHaveLength(1);

        const quote1 = await response1.json();
        expect(result.quotes[0].quoteId).toBe(quote1.quoteId);
    });
});
