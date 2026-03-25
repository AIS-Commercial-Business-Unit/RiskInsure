namespace RiskInsure.RatingAndUnderwriting.Domain.Managers;

using Models;
using Services;

public interface IQuoteManager
{
    Task<Quote> StartQuoteAsync(
        string quoteId,
        string customerId,
        decimal structureCoverageLimit,
        decimal structureDeductible,
        decimal contentsCoverageLimit,
        decimal contentsDeductible,
        int termMonths,
        DateTimeOffset effectiveDate);

    Task<Quote> SubmitUnderwritingAsync(
        string quoteId,
        UnderwritingSubmission submission,
        string zipCode);

    Task<Quote> AcceptQuoteAsync(string quoteId);
    Task<Quote> GetQuoteAsync(string quoteId);
    Task<IEnumerable<Quote>> GetCustomerQuotesAsync(string customerId);
    Task ExpireOldQuotesAsync();
}
