namespace RiskInsure.Billing.Domain.Managers.DTOs;

/// <summary>
/// DTO for updating the premium owed on an account
/// </summary>
public class UpdatePremiumOwedDto
{
    /// <summary>
    /// Account identifier
    /// </summary>
    public required string AccountId { get; set; }
    
    /// <summary>
    /// New premium amount owed
    /// </summary>
    public decimal NewPremiumOwed { get; set; }
    
    /// <summary>
    /// Reason for the premium change
    /// </summary>
    public required string ChangeReason { get; set; }
}
