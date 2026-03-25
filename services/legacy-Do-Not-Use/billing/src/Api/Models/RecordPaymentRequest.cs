namespace RiskInsure.Billing.Api.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Request model for recording a payment to a billing account
/// </summary>
public class RecordPaymentRequest
{
    /// <summary>
    /// Billing account identifier
    /// </summary>
    [Required(ErrorMessage = "AccountId is required")]
    public required string AccountId { get; set; }
    
    /// <summary>
    /// Payment amount to apply
    /// </summary>
    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Reference number from payment source (e.g., ACH trace number, transaction ID)
    /// </summary>
    [Required(ErrorMessage = "ReferenceNumber is required")]
    public required string ReferenceNumber { get; set; }
}
