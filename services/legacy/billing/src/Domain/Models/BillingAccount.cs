namespace RiskInsure.Billing.Domain.Models;

/// <summary>
/// Represents a billing account for tracking insurance premium payments
/// </summary>
public class BillingAccount
{
    /// <summary>
    /// Unique identifier for the billing account
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
    /// Policy holder name for display purposes
    /// </summary>
    public required string PolicyHolderName { get; set; }
    
    /// <summary>
    /// Current premium owed on the policy
    /// </summary>
    public decimal CurrentPremiumOwed { get; set; }
    
    /// <summary>
    /// Total premium amount due for the policy period (legacy - kept for compatibility)
    /// </summary>
    public decimal TotalPremiumDue { get; set; }
    
    /// <summary>
    /// Total amount paid to date
    /// </summary>
    public decimal TotalPaid { get; set; }
    
    /// <summary>
    /// Outstanding balance (calculated: CurrentPremiumOwed - TotalPaid)
    /// </summary>
    public decimal OutstandingBalance => CurrentPremiumOwed - TotalPaid;
    
    /// <summary>
    /// Current status of the billing account
    /// </summary>
    public BillingAccountStatus Status { get; set; }
    
    /// <summary>
    /// Billing cycle for the policy
    /// </summary>
    public BillingCycle BillingCycle { get; set; }
    
    /// <summary>
    /// When the policy became effective
    /// </summary>
    public DateTimeOffset EffectiveDate { get; set; }
    
    /// <summary>
    /// When the account was created
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
    
    /// <summary>
    /// When the account was last updated
    /// </summary>
    public DateTimeOffset LastUpdatedUtc { get; set; }
    
    /// <summary>
    /// ETag for optimistic concurrency control
    /// </summary>
    public string? ETag { get; set; }
}

/// <summary>
/// Status of a billing account
/// </summary>
public enum BillingAccountStatus
{
    /// <summary>
    /// Account created but not yet activated
    /// </summary>
    Pending,
    
    /// <summary>
    /// Account is active and accepting payments
    /// </summary>
    Active,
    
    /// <summary>
    /// Account is suspended (no payments accepted)
    /// </summary>
    Suspended,
    
    /// <summary>
    /// Account is fully paid
    /// </summary>
    PaidInFull,
    
    /// <summary>
    /// Account is past due
    /// </summary>
    PastDue,
    
    /// <summary>
    /// Account has been closed
    /// </summary>
    Closed
}

/// <summary>
/// Billing cycle options for a policy
/// </summary>
public enum BillingCycle
{
    /// <summary>
    /// Monthly billing cycle
    /// </summary>
    Monthly,
    
    /// <summary>
    /// Quarterly billing cycle (every 3 months)
    /// </summary>
    Quarterly,
    
    /// <summary>
    /// Semi-annual billing cycle (every 6 months)
    /// </summary>
    SemiAnnual,
    
    /// <summary>
    /// Annual billing cycle (once per year)
    /// </summary>
    Annual
}

