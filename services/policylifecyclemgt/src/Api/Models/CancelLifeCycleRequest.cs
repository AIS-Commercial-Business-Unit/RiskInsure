namespace RiskInsure.PolicyLifeCycleMgt.Api.Models;

using System.ComponentModel.DataAnnotations;

public class CancelLifeCycleRequest
{
    [Required]
    public DateTimeOffset CancellationDate { get; set; }

    [Required]
    [StringLength(500)]
    public required string Reason { get; set; }
}
