namespace RiskInsure.RiskRatingAndUnderwriting.Domain.Contracts.Events;

public record RiskQuoteCalculated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string QuoteId,
    decimal Premium,
    decimal BaseRate,
    decimal CoverageFactor,
    decimal TermFactor,
    decimal AgeFactor,
    decimal TerritoryFactor,
    string IdempotencyKey
);
