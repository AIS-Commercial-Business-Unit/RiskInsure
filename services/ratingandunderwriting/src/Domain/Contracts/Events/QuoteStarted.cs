namespace RiskInsure.RatingAndUnderwriting.Domain.Contracts.Events;

public record QuoteStarted(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string QuoteId,
    string CustomerId,
    decimal StructureCoverageLimit,
    decimal StructureDeductible,
    decimal ContentsCoverageLimit,
    decimal ContentsDeductible,
    int TermMonths,
    DateTimeOffset EffectiveDate,
    string IdempotencyKey
);
