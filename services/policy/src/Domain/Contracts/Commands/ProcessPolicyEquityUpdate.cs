namespace RiskInsure.Policy.Domain.Contracts.Commands;

public record ProcessPolicyEquityUpdate(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string IdempotencyKey,
    string PolicyId,
    string PolicyTermId,
    decimal EquityPercentage,
    decimal CancellationThresholdPercentage
);
