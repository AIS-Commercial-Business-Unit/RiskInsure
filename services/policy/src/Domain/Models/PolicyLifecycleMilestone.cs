namespace RiskInsure.Policy.Domain.Models;

using System.Text.Json.Serialization;

public class PolicyLifecycleMilestone
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("policyId")]
    public required string PolicyId { get; set; }

    [JsonPropertyName("policyTermId")]
    public required string PolicyTermId { get; set; }

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "PolicyLifecycleMilestone";

    [JsonPropertyName("milestoneType")]
    public required string MilestoneType { get; set; }

    [JsonPropertyName("occurredUtc")]
    public DateTimeOffset OccurredUtc { get; set; }

    [JsonPropertyName("processedMessageId")]
    public Guid ProcessedMessageId { get; set; }

    [JsonPropertyName("idempotencyKey")]
    public required string IdempotencyKey { get; set; }

    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; set; }
}
