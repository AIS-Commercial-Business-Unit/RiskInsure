namespace RiskInsure.Policy.Api.Models;

public class PolicySummary
{
    public required string PolicyId { get; set; }
    public required string PolicyNumber { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset EffectiveDate { get; set; }
    public DateTimeOffset ExpirationDate { get; set; }
    public decimal Premium { get; set; }
}

public class CustomerPoliciesResponse
{
    public List<PolicySummary> Policies { get; set; } = new();
}
