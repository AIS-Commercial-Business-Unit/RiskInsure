namespace RiskInsure.CustomerRelationshipsMgt.Domain.Validation;

using RiskInsure.CustomerRelationshipsMgt.Domain.Repositories;
using System.Text.RegularExpressions;

public interface IRelationshipValidator
{
    Task<ValidationResult> ValidateCreateRelationshipAsync(string email, DateTimeOffset birthDate, string zipCode);
    Task<ValidationResult> ValidateUpdateRelationshipAsync(string relationshipId);
    bool IsValidEmail(string email);
    bool IsValidZipCode(string zipCode);
    bool IsAgeEligible(DateTimeOffset birthDate);
}

public class RelationshipValidator : IRelationshipValidator
{
    private readonly IRelationshipRepository? _repository;
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex ZipCodeRegex = new(@"^\d{5}$", RegexOptions.Compiled);

    public RelationshipValidator(IRelationshipRepository? repository = null)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> ValidateCreateRelationshipAsync(string email, DateTimeOffset birthDate, string zipCode)
    {
        var result = new ValidationResult { IsValid = true };

        if (!IsValidEmail(email))
        {
            result.AddError("Email", "Email format is invalid");
        }
        else if (_repository != null)
        {
            var existingRelationship = await _repository.GetByEmailAsync(email);
            if (existingRelationship != null)
            {
                result.AddError("Email", "A relationship with this email address already exists");
            }
        }

        if (!IsAgeEligible(birthDate))
        {
            result.AddError("BirthDate", "Individual must be at least 18 years old");
        }

        if (!IsValidZipCode(zipCode))
        {
            result.AddError("ZipCode", "Zip code must be 5 digits");
        }

        return result;
    }

    public Task<ValidationResult> ValidateUpdateRelationshipAsync(string relationshipId)
    {
        // Future: Add update-specific validations
        return Task.FromResult(new ValidationResult { IsValid = true });
    }

    public bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        return EmailRegex.IsMatch(email);
    }

    public bool IsValidZipCode(string zipCode)
    {
        if (string.IsNullOrWhiteSpace(zipCode))
            return false;

        return ZipCodeRegex.IsMatch(zipCode);
    }

    public bool IsAgeEligible(DateTimeOffset birthDate)
    {
        var age = DateTimeOffset.UtcNow.Year - birthDate.Year;
        if (birthDate > DateTimeOffset.UtcNow.AddYears(-age))
        {
            age--;
        }
        return age >= 18;
    }
}
