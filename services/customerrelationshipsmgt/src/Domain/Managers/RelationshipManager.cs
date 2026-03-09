namespace RiskInsure.CustomerRelationshipsMgt.Domain.Managers;

using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.CustomerRelationshipsMgt.Domain.Contracts.Events;
using RiskInsure.CustomerRelationshipsMgt.Domain.Models;
using RiskInsure.CustomerRelationshipsMgt.Domain.Repositories;
using RiskInsure.CustomerRelationshipsMgt.Domain.Validation;

public interface IRelationshipManager
{
    Task<Relationship> CreateRelationshipAsync(string firstName, string lastName, string email, string phoneNumber, Address mailingAddress, DateTimeOffset? birthDate = null);
    Task<Relationship?> GetRelationshipAsync(string relationshipId);
    Task<Relationship> UpdateRelationshipAsync(string relationshipId, string? firstName, string? lastName, string? phoneNumber, Address? mailingAddress);
    Task CloseRelationshipAsync(string relationshipId);
    Task<bool> IsEmailUniqueAsync(string email);
}

public class RelationshipManager : IRelationshipManager
{
    private readonly IRelationshipRepository _repository;
    private readonly IRelationshipValidator _validator;
    private readonly IMessageSession _messageSession;
    private readonly ILogger<RelationshipManager> _logger;

    public RelationshipManager(
        IRelationshipRepository repository,
        IRelationshipValidator validator,
        IMessageSession messageSession,
        ILogger<RelationshipManager> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _messageSession = messageSession ?? throw new ArgumentNullException(nameof(messageSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Relationship> CreateRelationshipAsync(
        string firstName,
        string lastName,
        string email,
        string phoneNumber,
        Address mailingAddress,
        DateTimeOffset? birthDate = null)
    {
        var relationshipId = $"CRM-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        
        var validation = await _validator.ValidateCreateRelationshipAsync(email, birthDate ?? DateTimeOffset.MinValue, mailingAddress.ZipCode);
        if (!validation.IsValid)
        {
            _logger.LogWarning(
                "Relationship creation validation failed for email {Email}: {Errors}",
                email, string.Join(", ", validation.Errors.SelectMany(e => e.Value)));
            throw new ValidationException("Relationship validation failed", validation.Errors);
        }

        var relationship = new Relationship
        {
            Id = relationshipId,
            CustomerId = relationshipId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            MailingAddress = mailingAddress,
            BirthDate = birthDate ?? DateTimeOffset.MinValue,
            ZipCode = mailingAddress.ZipCode,
            Status = "Active",
            EmailVerified = false,
            MarketingOptIn = false,
            PreferredContactMethod = "Email"
        };

        var created = await _repository.CreateAsync(relationship);

        await _messageSession.Publish(new RelationshipCreated(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            RelationshipId: created.CustomerId,
            Email: created.Email,
            BirthDate: created.BirthDate,
            ZipCode: created.ZipCode,
            FirstName: created.FirstName,
            LastName: created.LastName,
            IdempotencyKey: Guid.NewGuid().ToString()
        ));

        _logger.LogInformation(
            "Relationship {RelationshipId} created with email {Email}",
            created.CustomerId, created.Email);

        return created;
    }

    public async Task<Relationship?> GetRelationshipAsync(string relationshipId)
    {
        return await _repository.GetByIdAsync(relationshipId);
    }

    public async Task<Relationship> UpdateRelationshipAsync(
        string relationshipId,
        string? firstName,
        string? lastName,
        string? phoneNumber,
        Address? mailingAddress)
    {
        var relationship = await _repository.GetByIdAsync(relationshipId);
        if (relationship == null)
        {
            throw new InvalidOperationException($"Relationship {relationshipId} not found");
        }

        var changedFields = new Dictionary<string, object>();

        if (firstName != null && relationship.FirstName != firstName)
        {
            relationship.FirstName = firstName;
            changedFields["FirstName"] = firstName;
        }

        if (lastName != null && relationship.LastName != lastName)
        {
            relationship.LastName = lastName;
            changedFields["LastName"] = lastName;
        }

        if (phoneNumber != null && relationship.PhoneNumber != phoneNumber)
        {
            relationship.PhoneNumber = phoneNumber;
            changedFields["PhoneNumber"] = phoneNumber;
        }

        if (mailingAddress != null)
        {
            relationship.MailingAddress = mailingAddress;
            changedFields["MailingAddress"] = mailingAddress;
        }

        var updated = await _repository.UpdateAsync(relationship);

        if (changedFields.Any())
        {
            await _messageSession.Publish(new RelationshipInformationUpdated(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                RelationshipId: updated.CustomerId,
                ChangedFields: changedFields,
                IdempotencyKey: Guid.NewGuid().ToString()
            ));
        }

        _logger.LogInformation(
            "Relationship {RelationshipId} updated: {ChangedFields}",
            relationshipId, string.Join(", ", changedFields.Keys));

        return updated;
    }

    public async Task CloseRelationshipAsync(string relationshipId)
    {
        var relationship = await _repository.GetByIdAsync(relationshipId);
        if (relationship == null)
        {
            throw new InvalidOperationException($"Relationship {relationshipId} not found");
        }

        relationship.Status = "Closed";
        relationship.Email = $"closed-{Guid.NewGuid()}@anonymized.local";
        relationship.PhoneNumber = null;
        relationship.MailingAddress = null;
        relationship.FirstName = null;
        relationship.LastName = null;

        await _repository.UpdateAsync(relationship);

        await _messageSession.Publish(new RelationshipClosed(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            RelationshipId: relationshipId,
            IdempotencyKey: Guid.NewGuid().ToString()
        ));

        _logger.LogInformation(
            "Relationship {RelationshipId} closed and anonymized",
            relationshipId);
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
