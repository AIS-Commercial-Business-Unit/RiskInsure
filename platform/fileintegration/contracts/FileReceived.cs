namespace RiskInsure.Contracts;

public record FileReceived(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid FileRunId,
    string FileName,
    long FileSize,
    string StorageUri,
    string IdempotencyKey
);
