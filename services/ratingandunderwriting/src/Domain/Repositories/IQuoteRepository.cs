namespace RiskInsure.RatingAndUnderwriting.Domain.Repositories;

using Models;

public interface IQuoteRepository
{
    Task<Quote?> GetByIdAsync(string quoteId);
    Task<IEnumerable<Quote>> GetByCustomerIdAsync(string customerId);
    Task<Quote> CreateAsync(Quote quote);
    Task<Quote> UpdateAsync(Quote quote);
    Task<IEnumerable<Quote>> GetExpirableQuotesAsync(DateTimeOffset currentDate);
}
