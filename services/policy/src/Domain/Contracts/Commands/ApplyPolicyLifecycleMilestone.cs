namespace RiskInsure.Policy.Domain.Contracts.Commands;

public record ApplyPolicyLifecycleMilestone(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string IdempotencyKey,
    string PolicyId,
    string PolicyTermId,
    string MilestoneType,
    DateTimeOffset MilestoneUtc
);
