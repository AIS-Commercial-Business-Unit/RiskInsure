namespace RiskInsure.PolicyEquityAndInvoicingMgt.Api.Models;

using System.ComponentModel.DataAnnotations;
using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Models;

/// <summary>
/// Request to update invoicing cycle
/// </summary>
public class UpdatePolicyEquityAndInvoicingCycleRequest
{
    /// <summary>
    /// New invoicing cycle
    /// </summary>
    [Required(ErrorMessage = "invoicing cycle is required")]
    public PolicyEquityAndInvoicingCycle NewPolicyEquityAndInvoicingCycle { get; set; }
    
    /// <summary>
    /// Reason for the invoicing cycle change
    /// </summary>
    [Required(ErrorMessage = "Change reason is required")]
    [StringLength(500, ErrorMessage = "Change reason cannot exceed 500 characters")]
    public string ChangeReason { get; set; } = string.Empty;
}
