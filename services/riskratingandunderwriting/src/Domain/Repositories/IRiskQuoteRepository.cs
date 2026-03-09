namespace RiskInsure.RiskRatingAndUnderwriting.Domain.Repositories;

using Models;

public interface IRiskQuoteRepository
{
    Task<Quote?> GetByIdAsync(string quoteId);
    Task<IEnumerable<Quote>> GetByCustomerIdAsync(string customerId);
    Task<Quote> CreateAsync(Quote quote);
    Task<Quote> UpdateAsync(Quote quote);
    Task<IEnumerable<Quote>> GetExpirableQuotesAsync(DateTimeOffset currentDate);
}
