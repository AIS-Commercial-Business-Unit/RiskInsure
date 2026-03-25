namespace RiskInsure.Billing.Api.Models;

using System.ComponentModel.DataAnnotations;
using RiskInsure.Billing.Domain.Models;

/// <summary>
/// Request to update billing cycle
/// </summary>
public class UpdateBillingCycleRequest
{
    /// <summary>
    /// New billing cycle
    /// </summary>
    [Required(ErrorMessage = "Billing cycle is required")]
    public BillingCycle NewBillingCycle { get; set; }
    
    /// <summary>
    /// Reason for the billing cycle change
    /// </summary>
    [Required(ErrorMessage = "Change reason is required")]
    [StringLength(500, ErrorMessage = "Change reason cannot exceed 500 characters")]
    public string ChangeReason { get; set; } = string.Empty;
}
