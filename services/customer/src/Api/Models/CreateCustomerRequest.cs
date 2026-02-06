namespace RiskInsure.Customer.Api.Models;

using System.ComponentModel.DataAnnotations;

public class CreateCustomerRequest
{
    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset BirthDate { get; set; }

    [Required]
    public string ZipCode { get; set; } = string.Empty;
}
