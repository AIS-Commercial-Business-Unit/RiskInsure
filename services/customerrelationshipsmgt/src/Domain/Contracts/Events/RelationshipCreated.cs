namespace RiskInsure.CustomerRelationshipsMgt.Domain.Contracts.Events;

public record RelationshipCreated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string RelationshipId,
    string Email,
    DateTimeOffset BirthDate,
    string ZipCode,
    string? FirstName,
    string? LastName,
    string IdempotencyKey
);
