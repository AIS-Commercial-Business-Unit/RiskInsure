namespace RiskInsure.CustomerRelationshipsMgt.Domain.Contracts.Events;

public record RelationshipClosed(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string RelationshipId,
    string IdempotencyKey
);
