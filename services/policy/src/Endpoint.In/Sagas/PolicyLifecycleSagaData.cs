namespace RiskInsure.Policy.Endpoint.In.Sagas;

using NServiceBus;

public class PolicyLifecycleSagaData : ContainSagaData
{
    public string PolicyId { get; set; } = string.Empty;

    public string PolicyTermId { get; set; } = string.Empty;

    public string CurrentStatus { get; set; } = string.Empty;

    public string? CompletionStatus { get; set; }

    public DateTimeOffset? CompletedUtc { get; set; }
}
