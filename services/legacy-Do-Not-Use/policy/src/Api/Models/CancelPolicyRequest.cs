namespace RiskInsure.Policy.Api.Models;

using System.ComponentModel.DataAnnotations;

public class CancelPolicyRequest
{
    [Required]
    public DateTimeOffset CancellationDate { get; set; }

    [Required]
    [StringLength(500)]
    public required string Reason { get; set; }
}
