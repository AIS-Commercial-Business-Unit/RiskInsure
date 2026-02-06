namespace RiskInsure.Customer.Domain.Managers;

using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.Customer.Domain.Contracts.Events;
using RiskInsure.Customer.Domain.Models;
using RiskInsure.Customer.Domain.Repositories;
using RiskInsure.Customer.Domain.Validation;

public interface ICustomerManager
{
    Task<Models.Customer> CreateCustomerAsync(string customerId, string email, DateTimeOffset birthDate, string zipCode);
    Task<Models.Customer?> GetCustomerAsync(string customerId);
    Task<Models.Customer> UpdateCustomerAsync(string customerId, string? firstName, string? lastName, string? phoneNumber, Address? mailingAddress);
    Task CloseCustomerAsync(string customerId);
    Task<bool> IsEmailUniqueAsync(string email);
}

public class CustomerManager : ICustomerManager
{
    private readonly ICustomerRepository _repository;
    private readonly ICustomerValidator _validator;
    private readonly IMessageSession _messageSession;
    private readonly ILogger<CustomerManager> _logger;

    public CustomerManager(
        ICustomerRepository repository,
        ICustomerValidator validator,
        IMessageSession messageSession,
        ILogger<CustomerManager> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _messageSession = messageSession ?? throw new ArgumentNullException(nameof(messageSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Models.Customer> CreateCustomerAsync(
        string customerId, 
        string email, 
        DateTimeOffset birthDate, 
        string zipCode)
    {
        var validation = await _validator.ValidateCreateCustomerAsync(email, birthDate, zipCode);
        if (!validation.IsValid)
        {
            _logger.LogWarning(
                "Customer creation validation failed for email {Email}: {Errors}",
                email, string.Join(", ", validation.Errors.SelectMany(e => e.Value)));
            throw new ValidationException("Customer validation failed", validation.Errors);
        }

        var customer = new Models.Customer
        {
            Id = customerId,
            CustomerId = customerId,
            Email = email,
            BirthDate = birthDate,
            ZipCode = zipCode,
            Status = "Active",
            EmailVerified = false,
            MarketingOptIn = false,
            PreferredContactMethod = "Email"
        };

        var created = await _repository.CreateAsync(customer);

        await _messageSession.Publish(new CustomerCreated(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            CustomerId: created.CustomerId,
            Email: created.Email,
            BirthDate: created.BirthDate,
            ZipCode: created.ZipCode,
            IdempotencyKey: Guid.NewGuid().ToString()
        ));

        _logger.LogInformation(
            "Customer {CustomerId} created with email {Email}",
            created.CustomerId, created.Email);

        return created;
    }

    public async Task<Models.Customer?> GetCustomerAsync(string customerId)
    {
        return await _repository.GetByIdAsync(customerId);
    }

    public async Task<Models.Customer> UpdateCustomerAsync(
        string customerId,
        string? firstName,
        string? lastName,
        string? phoneNumber,
        Address? mailingAddress)
    {
        var customer = await _repository.GetByIdAsync(customerId);
        if (customer == null)
        {
            throw new InvalidOperationException($"Customer {customerId} not found");
        }

        var changedFields = new Dictionary<string, object>();

        if (firstName != null && customer.FirstName != firstName)
        {
            customer.FirstName = firstName;
            changedFields["FirstName"] = firstName;
        }

        if (lastName != null && customer.LastName != lastName)
        {
            customer.LastName = lastName;
            changedFields["LastName"] = lastName;
        }

        if (phoneNumber != null && customer.PhoneNumber != phoneNumber)
        {
            customer.PhoneNumber = phoneNumber;
            changedFields["PhoneNumber"] = phoneNumber;
        }

        if (mailingAddress != null)
        {
            customer.MailingAddress = mailingAddress;
            changedFields["MailingAddress"] = mailingAddress;
        }

        var updated = await _repository.UpdateAsync(customer);

        if (changedFields.Any())
        {
            await _messageSession.Publish(new CustomerInformationUpdated(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                CustomerId: updated.CustomerId,
                ChangedFields: changedFields,
                IdempotencyKey: Guid.NewGuid().ToString()
            ));
        }

        _logger.LogInformation(
            "Customer {CustomerId} updated: {ChangedFields}",
            customerId, string.Join(", ", changedFields.Keys));

        return updated;
    }

    public async Task CloseCustomerAsync(string customerId)
    {
        var customer = await _repository.GetByIdAsync(customerId);
        if (customer == null)
        {
            throw new InvalidOperationException($"Customer {customerId} not found");
        }

        customer.Status = "Closed";
        customer.Email = $"closed-{Guid.NewGuid()}@anonymized.local";
        customer.PhoneNumber = null;
        customer.MailingAddress = null;
        customer.FirstName = null;
        customer.LastName = null;

        await _repository.UpdateAsync(customer);

        await _messageSession.Publish(new CustomerClosed(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            CustomerId: customerId,
            IdempotencyKey: Guid.NewGuid().ToString()
        ));

        _logger.LogInformation(
            "Customer {CustomerId} closed and anonymized",
            customerId);
    }

    public async Task<bool> IsEmailUniqueAsync(string email)
    {
        var existing = await _repository.GetByEmailAsync(email);
        return existing == null;
    }
}

public class ValidationException : Exception
{
    public Dictionary<string, List<string>> Errors { get; }

    public ValidationException(string message, Dictionary<string, List<string>> errors)
        : base(message)
    {
        Errors = errors;
    }
}
