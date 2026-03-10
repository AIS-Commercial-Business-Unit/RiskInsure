namespace RiskInsure.CustomerRelationshipsMgt.Domain.Contracts.Events;

public record RelationshipContactInformationChanged(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string RelationshipId,
    string ContactType,
    string? PreviousValue,
    string NewValue,
    string IdempotencyKey
);
