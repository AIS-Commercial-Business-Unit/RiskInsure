namespace RiskInsure.Customer.Api.Models;

using System.ComponentModel.DataAnnotations;

public class ChangeEmailRequest
{
    [Required]
    [EmailAddress]
    public string NewEmail { get; set; } = string.Empty;
}
