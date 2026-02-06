namespace RiskInsure.RatingAndUnderwriting.Domain.Contracts.Events;

public record UnderwritingSubmitted(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string QuoteId,
    int PriorClaimsCount,
    int KwegiboAge,
    string CreditTier,
    string? UnderwritingClass,
    string? DeclineReason,
    string IdempotencyKey
);
