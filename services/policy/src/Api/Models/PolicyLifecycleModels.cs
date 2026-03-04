namespace RiskInsure.Policy.Api.Models;

public class StartPolicyLifecycleRequest
{
    public required string PolicyTermId { get; set; }
    public DateTimeOffset EffectiveDateUtc { get; set; }
    public DateTimeOffset ExpirationDateUtc { get; set; }
    public int TermTicks { get; set; }
    public decimal RenewalOpenPercent { get; set; }
    public decimal? RenewalReminderPercent { get; set; }
    public decimal TermEndPercent { get; set; } = 100m;
    public decimal CancellationThresholdPercentage { get; set; } = -20m;
    public decimal GraceWindowPercent { get; set; } = 10m;
}

public class PolicyTermLifecycleResponse
{
    public required string PolicyId { get; set; }
    public required string PolicyTermId { get; set; }
    public required string CurrentStatus { get; set; }
    public List<string> StatusFlags { get; set; } = [];
    public decimal? CurrentEquityPercentage { get; set; }
    public decimal CancellationThresholdPercentage { get; set; }
    public DateTimeOffset? PendingCancellationStartedUtc { get; set; }
    public DateTimeOffset? GraceWindowRecheckUtc { get; set; }
    public DateTimeOffset EffectiveDateUtc { get; set; }
    public DateTimeOffset ExpirationDateUtc { get; set; }
    public string? CompletionStatus { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
}

public class PolicyEquitySignalRequest
{
    public required string PolicyTermId { get; set; }
    public decimal EquityPercentage { get; set; }
    public decimal CancellationThresholdPercentage { get; set; }
    public DateTimeOffset? OccurredUtc { get; set; }
}
