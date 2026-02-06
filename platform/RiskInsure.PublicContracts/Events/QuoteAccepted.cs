namespace RiskInsure.PublicContracts.Events;

public record QuoteAccepted(
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
    decimal Premium,
    string IdempotencyKey
);
