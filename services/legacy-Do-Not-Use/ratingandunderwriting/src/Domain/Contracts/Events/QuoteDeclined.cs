namespace RiskInsure.RatingAndUnderwriting.Domain.Contracts.Events;

public record QuoteDeclined(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string QuoteId,
    string DeclineReason,
    string IdempotencyKey
);
