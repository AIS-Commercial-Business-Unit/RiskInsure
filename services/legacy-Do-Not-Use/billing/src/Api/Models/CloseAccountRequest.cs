namespace RiskInsure.Billing.Api.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Request to close an account
/// </summary>
public class CloseAccountRequest
{
    /// <summary>
    /// Reason for closure
    /// </summary>
    [Required(ErrorMessage = "Closure reason is required")]
    [StringLength(500, ErrorMessage = "Closure reason cannot exceed 500 characters")]
    public string ClosureReason { get; set; } = string.Empty;
}
