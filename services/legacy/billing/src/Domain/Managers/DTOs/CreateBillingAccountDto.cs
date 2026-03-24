namespace RiskInsure.Billing.Domain.Managers.DTOs;

using RiskInsure.Billing.Domain.Models;

/// <summary>
/// DTO for creating a new billing account
/// </summary>
public class CreateBillingAccountDto
{
    /// <summary>
    /// Account identifier (caller-provided GUID)
    /// </summary>
    public required string AccountId { get; set; }
    
    /// <summary>
    /// Customer/policyholder identifier
    /// </summary>
    public required string CustomerId { get; set; }
    
    /// <summary>
    /// Policy number reference (must be unique)
    /// </summary>
    public required string PolicyNumber { get; set; }
    
    /// <summary>
    /// Policy holder name for display
    /// </summary>
    public required string PolicyHolderName { get; set; }
    
    /// <summary>
    /// Initial premium owed on the account
    /// </summary>
    public decimal CurrentPremiumOwed { get; set; }
    
    /// <summary>
    /// Billing cycle for the policy
    /// </summary>
    public BillingCycle BillingCycle { get; set; }
    
    /// <summary>
    /// When the policy becomes effective
    /// </summary>
    public DateTimeOffset EffectiveDate { get; set; }
}
