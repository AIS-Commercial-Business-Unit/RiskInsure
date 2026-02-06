namespace RiskInsure.Customer.Api.Models;

using System.Text.Json.Serialization;

public class CustomerResponse
{
    public string CustomerId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset? BirthDate { get; set; }
    public string ZipCode { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    
    [JsonPropertyName("phone")]
    public string? PhoneNumber { get; set; }
    
    [JsonPropertyName("address")]
    public Domain.Models.Address? MailingAddress { get; set; }
    
    public string Status { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
