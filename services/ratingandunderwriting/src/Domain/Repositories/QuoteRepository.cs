namespace RiskInsure.RatingAndUnderwriting.Domain.Repositories;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Models;

public class QuoteRepository : IQuoteRepository
{
    private readonly Container _container;
    private readonly ILogger<QuoteRepository> _logger;

    public QuoteRepository(Container container, ILogger<QuoteRepository> logger)
    {
        _container = container;
        _logger = logger;
    }

    public async Task<Quote?> GetByIdAsync(string quoteId)
    {
        try
        {
            var response = await _container.ReadItemAsync<Quote>(
                quoteId,
                new PartitionKey(quoteId));

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<Quote>> GetByCustomerIdAsync(string customerId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.customerId = @customerId AND c.documentType = 'Quote'")
            .WithParameter("@customerId", customerId);

        var iterator = _container.GetItemQueryIterator<Quote>(query);
        var quotes = new List<Quote>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            quotes.AddRange(response);
        }

        return quotes;
    }

    public async Task<Quote> CreateAsync(Quote quote)
    {
        quote.Id = quote.QuoteId;
        quote.CreatedUtc = DateTimeOffset.UtcNow;
        quote.UpdatedUtc = DateTimeOffset.UtcNow;

        var response = await _container.CreateItemAsync(
            quote,
            new PartitionKey(quote.QuoteId));

        _logger.LogInformation(
            "Created quote {QuoteId} for customer {CustomerId}",
            quote.QuoteId, quote.CustomerId);

        return response.Resource;
    }

    public async Task<Quote> UpdateAsync(Quote quote)
    {
        quote.UpdatedUtc = DateTimeOffset.UtcNow;

        var response = await _container.ReplaceItemAsync(
            quote,
            quote.Id,
            new PartitionKey(quote.QuoteId),
            new ItemRequestOptions { IfMatchEtag = quote.ETag });

        _logger.LogInformation(
            "Updated quote {QuoteId} with status {Status}",
            quote.QuoteId, quote.Status);

        return response.Resource;
    }

    public async Task<IEnumerable<Quote>> GetExpirableQuotesAsync(DateTimeOffset currentDate)
    {
        var query = new QueryDefinition(
            @"SELECT * FROM c 
              WHERE c.documentType = 'Quote' 
              AND c.expirationUtc < @currentDate 
              AND c.status != 'Accepted' 
              AND c.status != 'Expired'")
            .WithParameter("@currentDate", currentDate);

        var iterator = _container.GetItemQueryIterator<Quote>(query);
        var quotes = new List<Quote>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            quotes.AddRange(response);
        }

        return quotes;
    }
}
