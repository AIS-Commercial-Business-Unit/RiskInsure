namespace RiskInsure.CustomerRelationshipsMgt.Domain.Contracts.Events;

public record RelationshipInformationUpdated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string RelationshipId,
    Dictionary<string, object> ChangedFields,
    string IdempotencyKey
);
