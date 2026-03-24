namespace RiskInsure.Billing.Domain.Managers.DTOs;

using RiskInsure.Billing.Domain.Models;

/// <summary>
/// DTO for updating the billing cycle of an account
/// </summary>
public class UpdateBillingCycleDto
{
    /// <summary>
    /// Account identifier
    /// </summary>
    public required string AccountId { get; set; }
    
    /// <summary>
    /// New billing cycle
    /// </summary>
    public BillingCycle NewBillingCycle { get; set; }
    
    /// <summary>
    /// Reason for the billing cycle change
    /// </summary>
    public required string ChangeReason { get; set; }
}
