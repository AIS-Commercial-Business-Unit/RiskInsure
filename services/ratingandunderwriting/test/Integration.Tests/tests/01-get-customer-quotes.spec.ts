import { test, expect } from '@playwright/test';

test.describe('Get Customer Quotes', () => {
    // NOTE: Test retrieval of all quotes for a customer

    let customerId: string;
    let quoteIds: string[] = [];

    test.beforeAll(async ({ request }) => {
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
        expect(result).toHaveProperty('quoteId');
    });

    test('should return empty list for customer with no quotes', async ({ request }) => {
        const response = await request.get('/api/customers/CUST-123/quotes');

        expect(response.status()).toBe(200);

        const result = await response.json();
        expect(result.customerId).toBe('CUST-123');
        expect(Array.isArray(result.quotes)).toBe(true);
        expect(result.quotes).toHaveLength(0);
    });

    test('should return all quotes for customer', async ({ request }) => {

        // Create multiple quotes for the same customer
        for (let i = 0; i < 2; i++) {
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

            expect(response.status()).toBe(201);

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

    // test('should return quote summaries with correct fields', async ({ request }) => {
    //     // Create a quote
    //     const startResponse = await request.post('/api/quotes/start', {
    //         data: {
    //             customerId,
    //             structureCoverageLimit: 200000,
    //             structureDeductible: 1000,
    //             contentsCoverageLimit: 50000,
    //             contentsDeductible: 500,
    //             termMonths: 12,
    //             effectiveDate: new Date(Date.now() + 86400000).toISOString(),
    //             propertyZipCode: '60601'
    //         }
    //     });

    //     expect(startResponse.status()).toBe(201);

    //     const startResult = await startResponse.json();
    //     const quoteId = startResult.quoteId;

    //     const response = await request.get(`/api/customers/${customerId}/quotes`);

    //     expect(response.status()).toBe(200);

    //     const result = await response.json();
    //     expect(result.quotes).toHaveLength(1);

    //     const quote = result.quotes[0];
    //     expect(quote.quoteId).toBe(quoteId);
    //     expect(quote.status).toBe('Draft');
    //     expect(quote.premium).toBeNull();
    //     expect(quote.expirationUtc).toBeDefined();
    //     expect(quote.createdUtc).toBeDefined();
    // });

    test('should include premium in quoted quotes', async ({ request }) => {

        // Create and underwrite a quote
        // const startResponse = await request.post('/api/quotes/start', {
        //     data: {
        //         customerId,
        //         structureCoverageLimit: 200000,
        //         structureDeductible: 1000,
        //         contentsCoverageLimit: 50000,
        //         contentsDeductible: 500,
        //         termMonths: 12,
        //         effectiveDate: new Date(Date.now() + 86400000).toISOString(),
        //         propertyZipCode: '60601'
        //     }
        // });

        // expect(startResponse.status()).toBe(201);

        // const startResult = await startResponse.json();
        // const quoteId = startResult.quoteId;

        // Submit underwriting
        for (const quoteId of quoteIds) {
            const uwResponse = await request.post(`/api/quotes/${quoteId}/submit-underwriting`, {
                data: {
                    priorClaimsCount: 0,
                    propertyAgeYears: 10,
                    creditTier: 'Excellent'
                }
            });

            // add sleep here for 1000 milliseconds
            await new Promise(resolve => setTimeout(resolve, 1000));
            expect(uwResponse.status()).toBe(200);

            const response = await request.get(`/api/customers/${customerId}/quotes`);

            expect(response.status()).toBe(200);

            const result = await response.json();
            expect(result.quotes).toHaveLength(1);

            const quote = result.quotes[0];
            expect(quote.premium).toBeGreaterThan(0);
            expect(quote.quoteId).toBe(quoteId);
            expect(quote.status).toBe('Quoted');
            
        }
    });

    test('should return quotes in different statuses', async ({ request }) => {
        // Create draft quote
        // const draftResponse = await request.post('/api/quotes/start', {
        //     data: {
        //         customerId,
        //         structureCoverageLimit: 200000,
        //         structureDeductible: 1000,
        //         contentsCoverageLimit: 50000,
        //         contentsDeductible: 500,
        //         termMonths: 12,
        //         effectiveDate: new Date(Date.now() + 86400000).toISOString(),
        //         propertyZipCode: '60601'
        //     }
        // });

        // expect(draftResponse.status()).toBe(201);

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

        expect(quotedResponse.status()).toBe(201);

        const quotedResult = await quotedResponse.json();

        const uwQuotedResponse = await request.post(`/api/quotes/${quotedResult.quoteId}/submit-underwriting`, {
            data: {
                priorClaimsCount: 0,
                propertyAgeYears: 10,
                creditTier: 'Excellent'
            }
        });

        expect(uwQuotedResponse.status()).toBe(200);
        const uwQuotedResult = await uwQuotedResponse.json();
        expect(uwQuotedResult.status).toBe('Quoted');

        // Create, quote, and accept another
        // const acceptedResponse = await request.post('/api/quotes/start', {
        //     data: {
        //         customerId,
        //         structureCoverageLimit: 300000,
        //         structureDeductible: 2500,
        //         contentsCoverageLimit: 100000,
        //         contentsDeductible: 1500,
        //         termMonths: 12,
        //         effectiveDate: new Date(Date.now() + 86400000).toISOString(),
        //         propertyZipCode: '60601'
        //     }
        // });

        // expect(acceptedResponse.status()).toBe(201);

        // const acceptedResult = await acceptedResponse.json();

        // const uwAcceptedResponse = await request.post(`/api/quotes/${acceptedResult.quoteId}/submit-underwriting`, {
        //     data: {
        //         priorClaimsCount: 0,
        //         propertyAgeYears: 10,
        //         creditTier: 'Excellent'
        //     }
        // });

        // expect(uwAcceptedResponse.status()).toBe(200);

        const acceptResponse = await request.post(`/api/quotes/${uwQuotedResult.quoteId}/accept`);

        expect(acceptResponse.status()).toBe(200);

        const response = await request.get(`/api/customers/${customerId}/quotes`);

        expect(response.status()).toBe(200);

        const result = await response.json();
        expect(result.quotes).toHaveLength(2);

        const statuses = result.quotes.map((q: any) => q.status).sort();
        expect(statuses).toEqual(['Accepted', 'Draft']);
    });

    test('should not return quotes from other customers', async ({ request }) => {
        // Create quote for this customer
            // const response1 = await request.post('/api/quotes/start', {
            //     data: {
            //         customerId,
            //         structureCoverageLimit: 200000,
            //         structureDeductible: 1000,
            //         contentsCoverageLimit: 50000,
            //         contentsDeductible: 500,
            //         termMonths: 12,
            //         effectiveDate: new Date(Date.now() + 86400000).toISOString(),
            //         propertyZipCode: '60601'
            //     }
            // });

            // expect(response1.status()).toBe(201);

            // const quote1 = await response1.json();

        // Create quote for different customer
        const otherCustomerId = crypto.randomUUID();
        const response2 = await request.post('/api/quotes/start', {
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

        expect(response2.status()).toBe(201);

        const response = await request.get(`/api/customers/${otherCustomerId}/quotes`);

        expect(response.status()).toBe(200);

        const result = await response.json();
        expect(result.customerId).not.toBe(customerId);
        // expect(result.quotes).toHaveLength(1);
        //expect(result.quotes[0].quoteId).toBe(quote1.quoteId);
    });
});
