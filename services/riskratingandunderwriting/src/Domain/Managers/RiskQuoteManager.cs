namespace RiskInsure.RiskRatingAndUnderwriting.Domain.Managers;

using Contracts.Events;
using Microsoft.Extensions.Logging;
using Models;
using NServiceBus;
using Repositories;
using RiskInsure.PublicContracts.Events;
using Services;
using System.Diagnostics;

public class RiskQuoteManager : IRiskQuoteManager
{
    private static readonly ActivitySource ActivitySource = new("RiskInsure.RiskRatingAndUnderwriting.Publishing");
    private static readonly TimeSpan SlowPublishThreshold = TimeSpan.FromSeconds(2);

    private readonly IRiskQuoteRepository _repository;
    private readonly IRiskUnderwritingEngine _underwritingEngine;
    private readonly IRiskRatingEngine _ratingEngine;
    private readonly IMessageSession _messageSession;
    private readonly ILogger<RiskQuoteManager> _logger;

    public RiskQuoteManager(
        IRiskQuoteRepository repository,
        IRiskUnderwritingEngine underwritingEngine,
        IRiskRatingEngine ratingEngine,
        IMessageSession messageSession,
        ILogger<RiskQuoteManager> logger)
    {
        _repository = repository;
        _underwritingEngine = underwritingEngine;
        _ratingEngine = ratingEngine;
        _messageSession = messageSession;
        _logger = logger;
    }

    public async Task<Quote> StartQuoteAsync(
        string quoteId,
        string customerId,
        decimal structureCoverageLimit,
        decimal structureDeductible,
        decimal contentsCoverageLimit,
        decimal contentsDeductible,
        int termMonths,
        DateTimeOffset effectiveDate)
    {
        var quote = new Quote
        {
            QuoteId = quoteId,
            CustomerId = customerId,
            StructureCoverageLimit = structureCoverageLimit,
            StructureDeductible = structureDeductible,
            ContentsCoverageLimit = contentsCoverageLimit,
            ContentsDeductible = contentsDeductible,
            TermMonths = termMonths,
            EffectiveDate = effectiveDate,
            Status = "Draft",
            ExpirationUtc = DateTimeOffset.UtcNow.AddDays(30)
        };

        var created = await _repository.CreateAsync(quote);

        var quoteStarted = new RiskQuoteStarted(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            QuoteId: created.QuoteId,
            CustomerId: created.CustomerId,
            StructureCoverageLimit: created.StructureCoverageLimit,
            StructureDeductible: created.StructureDeductible,
            ContentsCoverageLimit: created.ContentsCoverageLimit,
            ContentsDeductible: created.ContentsDeductible,
            TermMonths: created.TermMonths,
            EffectiveDate: created.EffectiveDate,
            IdempotencyKey: $"RiskQuoteStarted-{created.QuoteId}"
        );

        await PublishWithDiagnosticsAsync(
            quoteStarted,
            quoteStarted.QuoteId,
            quoteStarted.IdempotencyKey);

        _logger.LogInformation(
            "Quote {QuoteId} started for customer {CustomerId}",
            created.QuoteId, created.CustomerId);

        return created;
    }

    public async Task<Quote> SubmitUnderwritingAsync(
        string quoteId,
        UnderwritingSubmission submission,
        string zipCode)
    {
        var quote = await _repository.GetByIdAsync(quoteId);
        if (quote == null)
        {
            throw new InvalidOperationException($"Quote {quoteId} not found");
        }

        if (quote.Status != "Draft")
        {
            throw new InvalidOperationException($"Quote {quoteId} is not in Draft status");
        }

        // Evaluate underwriting
        var result = _underwritingEngine.Evaluate(submission);

        quote.PriorClaimsCount = submission.PriorClaimsCount;
        quote.KwegiboAge = submission.KwegiboAge;
        quote.CreditTier = submission.CreditTier;
        quote.ZipCode = zipCode;

        // Publish underwriting submitted event
        var underwritingSubmitted = new UnderwritingRiskSubmitted(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            QuoteId: quote.QuoteId,
            PriorClaimsCount: submission.PriorClaimsCount,
            KwegiboAge: submission.KwegiboAge,
            CreditTier: submission.CreditTier,
            UnderwritingClass: result.UnderwritingClass,
            DeclineReason: result.DeclineReason,
            IdempotencyKey: $"UnderwritingRiskSubmitted-{quote.QuoteId}"
        );

        await PublishWithDiagnosticsAsync(
            underwritingSubmitted,
            underwritingSubmitted.QuoteId,
            underwritingSubmitted.IdempotencyKey);

        if (!result.IsApproved)
        {
            quote.Status = "Declined";
            quote.DeclineReason = result.DeclineReason;
            await _repository.UpdateAsync(quote);

            var quoteDeclined = new RiskQuoteDeclined(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                QuoteId: quote.QuoteId,
                DeclineReason: result.DeclineReason!,
                IdempotencyKey: $"RiskQuoteDeclined-{quote.QuoteId}"
            );

            await PublishWithDiagnosticsAsync(
                quoteDeclined,
                quoteDeclined.QuoteId,
                quoteDeclined.IdempotencyKey);

            _logger.LogWarning(
                "Quote {QuoteId} declined: {DeclineReason}",
                quote.QuoteId, result.DeclineReason);

            throw new InvalidOperationException($"Underwriting declined: {result.DeclineReason}");
        }

        // Approved - calculate premium
        quote.UnderwritingClass = result.UnderwritingClass;
        quote.Status = "Quoted";

        var breakdown = _ratingEngine.GetRatingBreakdown(quote, zipCode);
        quote.Premium = breakdown.Premium;
        quote.BaseRate = breakdown.BaseRate;
        quote.CoverageFactor = breakdown.CoverageFactor;
        quote.TermFactor = breakdown.TermFactor;
        quote.AgeFactor = breakdown.AgeFactor;
        quote.TerritoryFactor = breakdown.TerritoryFactor;

        var updated = await _repository.UpdateAsync(quote);

        var quoteCalculated = new RiskQuoteCalculated(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            QuoteId: updated.QuoteId,
            Premium: updated.Premium!.Value,
            BaseRate: updated.BaseRate!.Value,
            CoverageFactor: updated.CoverageFactor!.Value,
            TermFactor: updated.TermFactor!.Value,
            AgeFactor: updated.AgeFactor!.Value,
            TerritoryFactor: updated.TerritoryFactor!.Value,
            IdempotencyKey: $"RiskQuoteCalculated-{updated.QuoteId}"
        );

        await PublishWithDiagnosticsAsync(
            quoteCalculated,
            quoteCalculated.QuoteId,
            quoteCalculated.IdempotencyKey);

        _logger.LogInformation(
            "Quote {QuoteId} approved with class {Class} and premium {Premium}",
            updated.QuoteId, updated.UnderwritingClass, updated.Premium);

        return updated;
    }

    public async Task<Quote> AcceptQuoteAsync(string quoteId)
    {
        var quote = await _repository.GetByIdAsync(quoteId);
        if (quote == null)
        {
            throw new InvalidOperationException($"Quote {quoteId} not found");
        }

        if (quote.Status != "Quoted")
        {
            throw new InvalidOperationException($"Quote {quoteId} is not in Quoted status");
        }

        if (quote.ExpirationUtc < DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException($"Quote {quoteId} has expired");
        }

        if (!quote.Premium.HasValue)
        {
            throw new InvalidOperationException($"Quote {quoteId} has no calculated premium");
        }

        quote.Status = "Accepted";
        quote.AcceptedUtc = DateTimeOffset.UtcNow;

        var updated = await _repository.UpdateAsync(quote);

        _logger.LogInformation(
            "Publishing QuoteAccepted event for Quote {QuoteId}, Customer {CustomerId}",
            updated.QuoteId, updated.CustomerId);

        // Publish QuoteAccepted event (triggers policy creation in Policy domain)
        var quoteAccepted = new QuoteAccepted(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            QuoteId: updated.QuoteId,
            CustomerId: updated.CustomerId,
            StructureCoverageLimit: updated.StructureCoverageLimit,
            StructureDeductible: updated.StructureDeductible,
            ContentsCoverageLimit: updated.ContentsCoverageLimit,
            ContentsDeductible: updated.ContentsDeductible,
            TermMonths: updated.TermMonths,
            EffectiveDate: updated.EffectiveDate,
            Premium: updated.Premium!.Value, // Guaranteed non-null by validation above
            IdempotencyKey: $"QuoteAccepted-{updated.QuoteId}"
        );

        await PublishWithDiagnosticsAsync(
            quoteAccepted,
            quoteAccepted.QuoteId,
            quoteAccepted.IdempotencyKey);

        _logger.LogInformation(
            "QuoteAccepted event published for Quote {QuoteId}",
            updated.QuoteId);

        _logger.LogInformation(
            "Quote {QuoteId} accepted, policy creation initiated",
            updated.QuoteId);

        return updated;
    }

    public async Task<Quote> GetQuoteAsync(string quoteId)
    {
        var quote = await _repository.GetByIdAsync(quoteId);
        if (quote == null)
        {
            throw new InvalidOperationException($"Quote {quoteId} not found");
        }

        return quote;
    }

    public async Task<IEnumerable<Quote>> GetCustomerQuotesAsync(string customerId)
    {
        return await _repository.GetByCustomerIdAsync(customerId);
    }

    public async Task ExpireOldQuotesAsync()
    {
        var expirableQuotes = (await _repository.GetExpirableQuotesAsync(DateTimeOffset.UtcNow)).ToList();

        foreach (var quote in expirableQuotes)
        {
            quote.Status = "Expired";
            await _repository.UpdateAsync(quote);

            _logger.LogInformation(
                "Quote {QuoteId} expired",
                quote.QuoteId);
        }

        _logger.LogInformation(
            "Expired {Count} quotes",
            expirableQuotes.Count);
    }

    private async Task PublishWithDiagnosticsAsync<TEvent>(
        TEvent @event,
        string quoteId,
        string idempotencyKey)
        where TEvent : class
    {
        var eventName = typeof(TEvent).Name;
        var startedUtc = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        using var activity = ActivitySource.StartActivity("messaging.publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "nservicebus");
        activity?.SetTag("messaging.operation", "publish");
        activity?.SetTag("messaging.destination_kind", "topic");
        activity?.SetTag("messaging.message.type", eventName);
        activity?.SetTag("quote.id", quoteId);
        activity?.SetTag("message.idempotency_key", idempotencyKey);

        try
        {
            await _messageSession.Publish(@event);
            stopwatch.Stop();

            activity?.SetTag("publish.elapsed.ms", stopwatch.ElapsedMilliseconds);

            if (stopwatch.Elapsed > SlowPublishThreshold)
            {
                _logger.LogWarning(
                    "Slow event publish detected. Event {EventName} for Quote {QuoteId} took {ElapsedMs}ms (threshold {ThresholdMs}ms). IdempotencyKey {IdempotencyKey}. StartedUtc {StartedUtc}",
                    eventName,
                    quoteId,
                    stopwatch.ElapsedMilliseconds,
                    SlowPublishThreshold.TotalMilliseconds,
                    idempotencyKey,
                    startedUtc);
            }
            else
            {
                _logger.LogInformation(
                    "Event {EventName} published for Quote {QuoteId} in {ElapsedMs}ms. IdempotencyKey {IdempotencyKey}. StartedUtc {StartedUtc}",
                    eventName,
                    quoteId,
                    stopwatch.ElapsedMilliseconds,
                    idempotencyKey,
                    startedUtc);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetTag("publish.elapsed.ms", stopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            _logger.LogError(
                ex,
                "Event publish failed. Event {EventName} for Quote {QuoteId} failed after {ElapsedMs}ms. IdempotencyKey {IdempotencyKey}. StartedUtc {StartedUtc}",
                eventName,
                quoteId,
                stopwatch.ElapsedMilliseconds,
                idempotencyKey,
                startedUtc);

            throw;
        }
    }
}
