namespace RiskInsure.Billing.Api.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Request to suspend an account
/// </summary>
public class SuspendAccountRequest
{
    /// <summary>
    /// Reason for suspension
    /// </summary>
    [Required(ErrorMessage = "Suspension reason is required")]
    [StringLength(500, ErrorMessage = "Suspension reason cannot exceed 500 characters")]
    public string SuspensionReason { get; set; } = string.Empty;
}
