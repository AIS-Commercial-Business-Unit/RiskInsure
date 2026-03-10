namespace RiskInsure.CustomerRelationshipsMgt.Api.Models;

using RiskInsure.CustomerRelationshipsMgt.Domain.Models;

public class UpdateRelationshipRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public Address? MailingAddress { get; set; }
}
