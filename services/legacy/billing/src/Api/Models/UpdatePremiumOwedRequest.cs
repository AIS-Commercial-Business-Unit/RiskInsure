namespace RiskInsure.Billing.Api.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Request to update premium owed on an account
/// </summary>
public class UpdatePremiumOwedRequest
{
    /// <summary>
    /// New premium amount owed
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Premium must be >= 0")]
    public decimal NewPremiumOwed { get; set; }
    
    /// <summary>
    /// Reason for the premium change
    /// </summary>
    [Required(ErrorMessage = "Change reason is required")]
    [StringLength(500, ErrorMessage = "Change reason cannot exceed 500 characters")]
    public string ChangeReason { get; set; } = string.Empty;
}
