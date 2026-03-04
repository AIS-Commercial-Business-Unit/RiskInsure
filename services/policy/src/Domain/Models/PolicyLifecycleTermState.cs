namespace RiskInsure.Policy.Domain.Models;

using System.Text.Json.Serialization;

public class PolicyLifecycleTermState
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("policyId")]
    public required string PolicyId { get; set; }

    [JsonPropertyName("policyTermId")]
    public required string PolicyTermId { get; set; }

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "PolicyLifecycleTermState";

    [JsonPropertyName("currentStatus")]
    public required string CurrentStatus { get; set; }

    [JsonPropertyName("statusFlags")]
    public List<string> StatusFlags { get; set; } = [];

    [JsonPropertyName("effectiveDateUtc")]
    public DateTimeOffset EffectiveDateUtc { get; set; }

    [JsonPropertyName("expirationDateUtc")]
    public DateTimeOffset ExpirationDateUtc { get; set; }

    [JsonPropertyName("termTicks")]
    public int TermTicks { get; set; }

    [JsonPropertyName("renewalOpenPercent")]
    public decimal RenewalOpenPercent { get; set; }

    [JsonPropertyName("renewalReminderPercent")]
    public decimal? RenewalReminderPercent { get; set; }

    [JsonPropertyName("termEndPercent")]
    public decimal TermEndPercent { get; set; }

    [JsonPropertyName("currentEquityPercentage")]
    public decimal? CurrentEquityPercentage { get; set; }

    [JsonPropertyName("cancellationThresholdPercentage")]
    public decimal CancellationThresholdPercentage { get; set; }

    [JsonPropertyName("graceWindowPercent")]
    public decimal GraceWindowPercent { get; set; }

    [JsonPropertyName("pendingCancellationStartedUtc")]
    public DateTimeOffset? PendingCancellationStartedUtc { get; set; }

    [JsonPropertyName("graceWindowRecheckUtc")]
    public DateTimeOffset? GraceWindowRecheckUtc { get; set; }

    [JsonPropertyName("completionStatus")]
    public string? CompletionStatus { get; set; }

    [JsonPropertyName("completedUtc")]
    public DateTimeOffset? CompletedUtc { get; set; }

    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; set; }

    [JsonPropertyName("updatedUtc")]
    public DateTimeOffset UpdatedUtc { get; set; }

    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
