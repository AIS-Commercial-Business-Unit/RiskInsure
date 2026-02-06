namespace RiskInsure.Customer.Domain.Contracts.Events;

public record CustomerCreated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string CustomerId,
    string Email,
    DateTimeOffset BirthDate,
    string ZipCode,
    string IdempotencyKey
);
