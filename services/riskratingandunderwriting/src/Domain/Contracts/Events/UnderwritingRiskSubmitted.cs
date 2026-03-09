namespace RiskInsure.RiskRatingAndUnderwriting.Domain.Contracts.Events;

public record UnderwritingRiskSubmitted(
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
