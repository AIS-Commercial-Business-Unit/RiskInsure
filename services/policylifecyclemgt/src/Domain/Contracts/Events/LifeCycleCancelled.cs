namespace RiskInsure.PolicyLifeCycleMgt.Domain.Contracts.Events;

public record LifeCycleCancelled(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyId,
    string CustomerId,
    DateTimeOffset CancellationDate,
    string CancellationReason,
    decimal UnearnedPremium,
    string IdempotencyKey
);
