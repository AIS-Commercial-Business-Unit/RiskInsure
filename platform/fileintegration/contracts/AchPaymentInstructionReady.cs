namespace RiskInsure.Contracts;

public record AchPaymentInstructionReady(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid FileRunId,
    string PaymentInstructionId,
    string BatchId,
    string TraceNumber,
    string IdempotencyKey,
    string PayloadRef
);
