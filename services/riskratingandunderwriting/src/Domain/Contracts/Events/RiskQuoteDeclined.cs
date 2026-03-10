namespace RiskInsure.RiskRatingAndUnderwriting.Domain.Contracts.Events;

public record RiskQuoteDeclined(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string QuoteId,
    string DeclineReason,
    string IdempotencyKey
);
