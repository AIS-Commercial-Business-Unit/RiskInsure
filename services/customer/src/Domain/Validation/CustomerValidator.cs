namespace RiskInsure.Customer.Domain.Validation;

using RiskInsure.Customer.Domain.Repositories;
using System.Text.RegularExpressions;

public interface ICustomerValidator
{
    Task<ValidationResult> ValidateCreateCustomerAsync(string email, DateTimeOffset birthDate, string zipCode);
    Task<ValidationResult> ValidateUpdateCustomerAsync(string customerId);
    bool IsValidEmail(string email);
    bool IsValidZipCode(string zipCode);
    bool IsAgeEligible(DateTimeOffset birthDate);
}

public class CustomerValidator : ICustomerValidator
{
    private readonly ICustomerRepository? _repository;
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex ZipCodeRegex = new(@"^\d{5}$", RegexOptions.Compiled);

    public CustomerValidator(ICustomerRepository? repository = null)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> ValidateCreateCustomerAsync(string email, DateTimeOffset birthDate, string zipCode)
    {
        var result = new ValidationResult { IsValid = true };

        if (!IsValidEmail(email))
        {
            result.AddError("Email", "Email format is invalid");
        }
        else if (_repository != null)
        {
            var existingCustomer = await _repository.GetByEmailAsync(email);
            if (existingCustomer != null)
            {
                result.AddError("Email", "A customer with this email address already exists");
            }
        }

        if (!IsAgeEligible(birthDate))
        {
            result.AddError("BirthDate", "Customer must be at least 18 years old");
        }

        if (!IsValidZipCode(zipCode))
        {
            result.AddError("ZipCode", "Zip code must be 5 digits");
        }

        return result;
    }

    public Task<ValidationResult> ValidateUpdateCustomerAsync(string customerId)
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
