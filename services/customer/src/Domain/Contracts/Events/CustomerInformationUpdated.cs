namespace RiskInsure.Customer.Domain.Contracts.Events;

public record CustomerInformationUpdated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string CustomerId,
    Dictionary<string, object> ChangedFields,
    string IdempotencyKey
);
