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
    /// Policy number reference
    /// </summary>
    public required string PolicyNumber { get; set; }
    
    /// <summary>
    /// Total premium amount due for the policy period
    /// </summary>
    public decimal TotalPremiumDue { get; set; }
    
    /// <summary>
    /// Total amount paid to date
    /// </summary>
    public decimal TotalPaid { get; set; }
    
    /// <summary>
    /// Outstanding balance (calculated: TotalPremiumDue - TotalPaid)
    /// </summary>
    public decimal OutstandingBalance => TotalPremiumDue - TotalPaid;
    
    /// <summary>
    /// Current status of the billing account
    /// </summary>
    public BillingAccountStatus Status { get; set; }
    
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
    /// Account is active and accepting payments
    /// </summary>
    Active,
    
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
