namespace RiskInsure.Customer.Api.Models;

using RiskInsure.Customer.Domain.Models;

public class UpdateCustomerRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public Address? MailingAddress { get; set; }
}
