namespace RiskInsure.Policy.Domain.Contracts.Events;

public record PolicyCancelled(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyId,
    string CustomerId,
    DateTimeOffset CancellationDate,
    string CancellationReason,
    decimal UnearnedPremium,
    string IdempotencyKey
);
