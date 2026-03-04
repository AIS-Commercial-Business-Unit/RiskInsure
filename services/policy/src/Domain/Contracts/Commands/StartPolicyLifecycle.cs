namespace RiskInsure.Policy.Domain.Contracts.Commands;

public record StartPolicyLifecycle(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string IdempotencyKey,
    string PolicyId,
    string PolicyTermId,
    int TermTicks,
    DateTimeOffset EffectiveDate,
    DateTimeOffset ExpirationDate,
    decimal RenewalOpenPercent,
    decimal? RenewalReminderPercent,
    decimal TermEndPercent,
    decimal CancellationThresholdPercentage,
    decimal GraceWindowPercent
);
