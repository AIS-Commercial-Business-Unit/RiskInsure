namespace RiskInsure.Billing.Api.Models;

/// <summary>
/// Response model for billing account information
/// </summary>
public class BillingAccountResponse
{
    /// <summary>
    /// Unique account identifier
    /// </summary>
    public required string AccountId { get; set; }
    
    /// <summary>
    /// Customer/policyholder identifier
    /// </summary>
    public required string CustomerId { get; set; }
    
    /// <summary>
    /// Policy number reference
    /// </summary>
    public required string PolicyNumber { get; set; }
    
    /// <summary>
    /// Policy holder name
    /// </summary>
    public required string PolicyHolderName { get; set; }
    
    /// <summary>
    /// Current premium owed
    /// </summary>
    public decimal CurrentPremiumOwed { get; set; }
    
    /// <summary>
    /// Total amount paid to date
    /// </summary>
    public decimal TotalPaid { get; set; }
    
    /// <summary>
    /// Outstanding balance
    /// </summary>
    public decimal OutstandingBalance { get; set; }
    
    /// <summary>
    /// Current account status (Pending, Active, Suspended, Closed)
    /// </summary>
    public required string Status { get; set; }
    
    /// <summary>
    /// Billing cycle (Monthly, Quarterly, Annual)
    /// </summary>
    public string? BillingCycle { get; set; }
    
    /// <summary>
    /// Policy effective date
    /// </summary>
    public DateTimeOffset EffectiveDate { get; set; }
    
    /// <summary>
    /// Account creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
    
    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTimeOffset LastUpdatedUtc { get; set; }
}
