namespace RiskInsure.RatingAndUnderwriting.Api.Controllers;

using Domain.Managers;
using Domain.Services;
using Microsoft.AspNetCore.Mvc;
using Models;

[ApiController]
[Route("api/quotes")]
[Produces("application/json")]
public class QuotesController : ControllerBase
{
    private readonly IQuoteManager _manager;
    private readonly ILogger<QuotesController> _logger;

    public QuotesController(
        IQuoteManager manager,
        ILogger<QuotesController> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    [HttpPost("start")]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartQuote([FromBody] StartQuoteRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var quoteId = $"QUOTE-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        var quote = await _manager.StartQuoteAsync(
            quoteId,
            request.CustomerId,
            request.StructureCoverageLimit,
            request.StructureDeductible,
            request.ContentsCoverageLimit ?? 0,
            request.ContentsDeductible ?? 0,
            request.TermMonths,
            request.EffectiveDate);

        return CreatedAtAction(
            nameof(GetQuote),
            new { quoteId = quote.QuoteId },
            new QuoteResponse
            {
                QuoteId = quote.QuoteId,
                CustomerId = quote.CustomerId,
                Status = quote.Status,
                StructureCoverageLimit = quote.StructureCoverageLimit,
                StructureDeductible = quote.StructureDeductible,
                ContentsCoverageLimit = quote.ContentsCoverageLimit,
                ContentsDeductible = quote.ContentsDeductible,
                TermMonths = quote.TermMonths,
                EffectiveDate = quote.EffectiveDate,
                Premium = quote.Premium,
                UnderwritingClass = quote.UnderwritingClass,
                ExpirationUtc = quote.ExpirationUtc,
                CreatedUtc = quote.CreatedUtc
            });
    }

    [HttpPost("{quoteId}/submit-underwriting")]
    [ProducesResponseType(typeof(SubmitUnderwritingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitUnderwriting(
        string quoteId,
        [FromBody] SubmitUnderwritingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var submission = new UnderwritingSubmission(
                request.PriorClaimsCount,
                request.PropertyAgeYears,
                request.CreditTier);

            var quote = await _manager.SubmitUnderwritingAsync(quoteId, submission, request.PropertyAgeYears.ToString());

            return Ok(new SubmitUnderwritingResponse
            {
                QuoteId = quote.QuoteId,
                Status = quote.Status,
                UnderwritingClass = quote.UnderwritingClass,
                Premium = quote.Premium,
                ExpirationUtc = quote.ExpirationUtc
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = "QuoteNotFound", message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("declined"))
        {
            return UnprocessableEntity(new { error = "UnderwritingDeclined", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = "InvalidQuoteStatus", message = ex.Message });
        }
    }

    [HttpPost("{quoteId}/accept")]
    [ProducesResponseType(typeof(AcceptQuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcceptQuote(string quoteId)
    {
        try
        {
            var quote = await _manager.AcceptQuoteAsync(quoteId);

            return Ok(new AcceptQuoteResponse
            {
                QuoteId = quote.QuoteId,
                Status = quote.Status,
                AcceptedUtc = quote.AcceptedUtc!.Value,
                Premium = quote.Premium!.Value,
                Message = "Quote accepted. Policy creation initiated.",
                PolicyCreationInitiated = true
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = "QuoteNotFound", message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("expired"))
        {
            return BadRequest(new { error = "QuoteExpired", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = "InvalidQuoteStatus", message = ex.Message });
        }
    }

    [HttpGet("{quoteId}")]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQuote(string quoteId)
    {
        try
        {
            var quote = await _manager.GetQuoteAsync(quoteId);

            return Ok(new QuoteResponse
            {
                QuoteId = quote.QuoteId,
                CustomerId = quote.CustomerId,
                Status = quote.Status,
                StructureCoverageLimit = quote.StructureCoverageLimit,
                StructureDeductible = quote.StructureDeductible,
                ContentsCoverageLimit = quote.ContentsCoverageLimit,
                ContentsDeductible = quote.ContentsDeductible,
                TermMonths = quote.TermMonths,
                EffectiveDate = quote.EffectiveDate,
                Premium = quote.Premium,
                UnderwritingClass = quote.UnderwritingClass,
                ExpirationUtc = quote.ExpirationUtc,
                CreatedUtc = quote.CreatedUtc
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = "QuoteNotFound", message = ex.Message });
        }
    }

    [HttpGet("/api/customers/{customerId}/quotes")]
    [ProducesResponseType(typeof(CustomerQuotesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerQuotes(string customerId)
    {
        var quotes = await _manager.GetCustomerQuotesAsync(customerId);

        return Ok(new CustomerQuotesResponse
        {
            CustomerId = customerId,
            Quotes = quotes.Select(q => new QuoteSummary
            {
                QuoteId = q.QuoteId,
                Status = q.Status,
                Premium = q.Premium,
                ExpirationUtc = q.ExpirationUtc,
                CreatedUtc = q.CreatedUtc
            }).ToList()
        });
    }
}
