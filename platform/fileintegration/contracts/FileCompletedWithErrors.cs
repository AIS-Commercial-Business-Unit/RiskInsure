namespace RiskInsure.Contracts;

public record FileCompletedWithErrors(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid FileRunId,
    int TotalInstructions,
    int SucceededCount,
    int FailedCount,
    string IdempotencyKey
);
