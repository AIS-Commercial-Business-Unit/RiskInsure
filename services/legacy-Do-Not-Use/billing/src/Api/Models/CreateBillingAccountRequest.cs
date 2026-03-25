namespace RiskInsure.Billing.Api.Models;

using System.ComponentModel.DataAnnotations;
using RiskInsure.Billing.Domain.Models;

/// <summary>
/// Request to create a new billing account
/// </summary>
public class CreateBillingAccountRequest
{
    /// <summary>
    /// Account identifier (client-generated GUID)
    /// </summary>
    [Required(ErrorMessage = "Account ID is required")]
    public string AccountId { get; set; } = string.Empty;
    
    /// <summary>
    /// Customer/policyholder identifier
    /// </summary>
    [Required(ErrorMessage = "Customer ID is required")]
    public string CustomerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Policy number (must be unique)
    /// </summary>
    [Required(ErrorMessage = "Policy number is required")]
    [StringLength(50, ErrorMessage = "Policy number cannot exceed 50 characters")]
    public string PolicyNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Policy holder name
    /// </summary>
    [Required(ErrorMessage = "Policy holder name is required")]
    [StringLength(200, ErrorMessage = "Policy holder name cannot exceed 200 characters")]
    public string PolicyHolderName { get; set; } = string.Empty;
    
    /// <summary>
    /// Current premium owed on the account
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Premium must be >= 0")]
    public decimal CurrentPremiumOwed { get; set; }
    
    /// <summary>
    /// Billing cycle for the policy
    /// </summary>
    [Required(ErrorMessage = "Billing cycle is required")]
    public BillingCycle BillingCycle { get; set; }
    
    /// <summary>
    /// When the policy becomes effective
    /// </summary>
    [Required(ErrorMessage = "Effective date is required")]
    public DateTimeOffset EffectiveDate { get; set; }
}
