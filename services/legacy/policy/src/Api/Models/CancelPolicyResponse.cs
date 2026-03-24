namespace RiskInsure.Policy.Api.Models;

public class CancelPolicyResponse
{
    public required string PolicyId { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CancellationDate { get; set; }
    public decimal UnearnedPremium { get; set; }
}
