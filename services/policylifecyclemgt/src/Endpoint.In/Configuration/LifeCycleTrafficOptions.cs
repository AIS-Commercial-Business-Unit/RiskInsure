namespace RiskInsure.PolicyLifeCycleMgt.Endpoint.In.Configuration;

public class LifeCycleTrafficOptions
{
    public const string SectionName = "TrafficRouting";

    public bool EnableLifeCycleProcessing { get; set; }

    // Percentage from 0 to 100 used for deterministic quote-based routing.
    public int LifeCycleProcessingPercentage { get; set; }
}
