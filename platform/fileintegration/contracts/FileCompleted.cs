namespace RiskInsure.Contracts;

public record FileCompleted(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid FileRunId,
    int TotalInstructions,
    string IdempotencyKey
);
