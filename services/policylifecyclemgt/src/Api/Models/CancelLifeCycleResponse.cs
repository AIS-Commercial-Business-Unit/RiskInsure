namespace RiskInsure.PolicyLifeCycleMgt.Api.Models;

public class CancelLifeCycleResponse
{
    public required string PolicyId { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CancellationDate { get; set; }
    public decimal UnearnedPremium { get; set; }
}
