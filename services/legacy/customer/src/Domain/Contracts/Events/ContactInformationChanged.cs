namespace RiskInsure.Customer.Domain.Contracts.Events;

public record ContactInformationChanged(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string CustomerId,
    string ContactType,
    string? PreviousValue,
    string NewValue,
    string IdempotencyKey
);
