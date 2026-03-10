namespace RiskInsure.CustomerRelationshipsMgt.Unit.Tests;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NServiceBus;
using RiskInsure.CustomerRelationshipsMgt.Domain.Contracts.Events;
using RiskInsure.CustomerRelationshipsMgt.Domain.Managers;
using RiskInsure.CustomerRelationshipsMgt.Domain.Models;
using RiskInsure.CustomerRelationshipsMgt.Domain.Repositories;
using RiskInsure.CustomerRelationshipsMgt.Domain.Validation;
using Xunit;

public class RelationshipManagerTests
{
    private readonly Mock<IRelationshipRepository> _mockRepository;
    private readonly Mock<IRelationshipValidator> _mockValidator;
    private readonly Mock<IMessageSession> _mockMessageSession;
    private readonly Mock<ILogger<RelationshipManager>> _mockLogger;
    private readonly RelationshipManager _manager;

    public RelationshipManagerTests()
    {
        _mockRepository = new Mock<IRelationshipRepository>();
        _mockValidator = new Mock<IRelationshipValidator>();
        _mockMessageSession = new Mock<IMessageSession>();
        _mockLogger = new Mock<ILogger<RelationshipManager>>();
        _manager = new RelationshipManager(
            _mockRepository.Object,
            _mockValidator.Object,
            _mockMessageSession.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task CreateRelationshipAsync_ValidData_CreatesRelationshipAndPublishesEvent()
    {
        // Arrange
        var firstName = "John";
        var lastName = "Doe";
        var email = "test@example.com";
        var phoneNumber = "+1-555-0100";
        var birthDate = DateTimeOffset.UtcNow.AddYears(-25);
        var mailingAddress = new Address
        {
            Street = "123 Main St",
            City = "Beverly Hills",
            State = "CA",
            ZipCode = "90210"
        };

        _mockValidator.Setup(v => v.ValidateCreateRelationshipAsync(email, birthDate, mailingAddress.ZipCode))
            .ReturnsAsync(new ValidationResult { IsValid = true });

        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<Relationship>()))
            .ReturnsAsync((Relationship c) => c);

        // Act
        var result = await _manager.CreateRelationshipAsync(firstName, lastName, email, phoneNumber, mailingAddress, birthDate);

        // Assert
        result.Should().NotBeNull();
        result.CustomerId.Should().StartWith("CRM-");
        result.FirstName.Should().Be(firstName);
        result.LastName.Should().Be(lastName);
        result.Email.Should().Be(email);
        result.PhoneNumber.Should().Be(phoneNumber);
        result.Status.Should().Be("Active");

        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<Relationship>()), Times.Once);
        _mockMessageSession.Verify(
            m => m.Publish(It.IsAny<RelationshipCreated>(), It.IsAny<PublishOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateRelationshipAsync_InvalidEmail_ThrowsValidationException()
    {
        // Arrange
        var firstName = "John";
        var lastName = "Doe";
        var email = "invalid-email";
        var phoneNumber = "+1-555-0100";
        var birthDate = DateTimeOffset.UtcNow.AddYears(-25);
        var mailingAddress = new Address
        {
            Street = "123 Main St",
            City = "Beverly Hills",
            State = "CA",
            ZipCode = "90210"
        };

        var validationResult = new ValidationResult { IsValid = false };
        validationResult.AddError("Email", "Email format is invalid");

        _mockValidator.Setup(v => v.ValidateCreateRelationshipAsync(email, birthDate, mailingAddress.ZipCode))
            .ReturnsAsync(validationResult);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _manager.CreateRelationshipAsync(firstName, lastName, email, phoneNumber, mailingAddress, birthDate));

        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<Relationship>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRelationshipAsync_ValidData_UpdatesRelationshipAndPublishesEvent()
    {
        // Arrange
        var relationshipId = Guid.NewGuid().ToString();
        var existingRelationship = new Relationship
        {
            Id = relationshipId,
            CustomerId = relationshipId,
            Email = "test@example.com",
            FirstName = "John"
        };

        _mockRepository.Setup(r => r.GetByIdAsync(relationshipId))
            .ReturnsAsync(existingRelationship);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Relationship>()))
            .ReturnsAsync((Relationship c) => c);

        // Act
        var result = await _manager.UpdateRelationshipAsync(
            relationshipId,
            "Jane",
            "Doe",
            "+1-555-1234",
            null);

        // Assert
        result.Should().NotBeNull();
        result.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Doe");
        result.PhoneNumber.Should().Be("+1-555-1234");

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<Relationship>()), Times.Once);
        _mockMessageSession.Verify(
            m => m.Publish(It.IsAny<RelationshipInformationUpdated>(), It.IsAny<PublishOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task CloseRelationshipAsync_ExistingRelationship_AnonymizesDataAndPublishesEvent()
    {
        // Arrange
        var relationshipId = Guid.NewGuid().ToString();
        var existingRelationship = new Relationship
        {
            Id = relationshipId,
            CustomerId = relationshipId,
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            PhoneNumber = "+1-555-1234",
            Status = "Active"
        };

        _mockRepository.Setup(r => r.GetByIdAsync(relationshipId))
            .ReturnsAsync(existingRelationship);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Relationship>()))
            .ReturnsAsync((Relationship c) => c);

        // Act
        await _manager.CloseRelationshipAsync(relationshipId);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<Relationship>(c =>
            c.Status == "Closed" &&
            c.FirstName == null &&
            c.LastName == null &&
            c.PhoneNumber == null &&
            c.Email.Contains("anonymized"))), Times.Once);

        _mockMessageSession.Verify(
            m => m.Publish(It.IsAny<RelationshipClosed>(), It.IsAny<PublishOptions>()),
            Times.Once);
    }
}
