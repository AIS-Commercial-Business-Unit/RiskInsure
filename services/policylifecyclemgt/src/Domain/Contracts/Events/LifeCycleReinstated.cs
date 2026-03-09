namespace RiskInsure.PolicyLifeCycleMgt.Domain.Contracts.Events;

public record LifeCycleReinstated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyId,
    string PolicyNumber,
    string CustomerId,
    decimal PaymentAmount,
    string IdempotencyKey
);
