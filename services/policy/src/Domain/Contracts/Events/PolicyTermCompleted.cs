namespace RiskInsure.Policy.Domain.Contracts.Events;

public record PolicyTermCompleted(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyId,
    string PolicyTermId,
    string CompletionStatus,
    DateTimeOffset CompletedUtc,
    string IdempotencyKey
);
