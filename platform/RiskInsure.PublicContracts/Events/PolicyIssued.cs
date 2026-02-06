namespace RiskInsure.PublicContracts.Events;

public record PolicyIssued(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyId,
    string PolicyNumber,
    string CustomerId,
    decimal StructureCoverageLimit,
    decimal StructureDeductible,
    decimal ContentsCoverageLimit,
    decimal ContentsDeductible,
    int TermMonths,
    DateTimeOffset EffectiveDate,
    DateTimeOffset ExpirationDate,
    decimal Premium,
    string IdempotencyKey
);
