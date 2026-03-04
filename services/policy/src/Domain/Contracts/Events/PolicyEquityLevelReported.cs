namespace RiskInsure.Policy.Domain.Contracts.Events;

public record PolicyEquityLevelReported(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyId,
    string PolicyTermId,
    decimal EquityPercentage,
    decimal CancellationThresholdPercentage,
    bool CancellationRecommended,
    string IdempotencyKey
);
