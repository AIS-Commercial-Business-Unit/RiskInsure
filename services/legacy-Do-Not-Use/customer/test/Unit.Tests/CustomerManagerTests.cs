namespace RiskInsure.Customer.Unit.Tests;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NServiceBus;
using RiskInsure.Customer.Domain.Contracts.Events;
using RiskInsure.Customer.Domain.Managers;
using RiskInsure.Customer.Domain.Models;
using RiskInsure.Customer.Domain.Repositories;
using RiskInsure.Customer.Domain.Validation;
using Xunit;

public class CustomerManagerTests
{
    private readonly Mock<ICustomerRepository> _mockRepository;
    private readonly Mock<ICustomerValidator> _mockValidator;
    private readonly Mock<IMessageSession> _mockMessageSession;
    private readonly Mock<ILogger<CustomerManager>> _mockLogger;
    private readonly CustomerManager _manager;

    public CustomerManagerTests()
    {
        _mockRepository = new Mock<ICustomerRepository>();
        _mockValidator = new Mock<ICustomerValidator>();
        _mockMessageSession = new Mock<IMessageSession>();
        _mockLogger = new Mock<ILogger<CustomerManager>>();
        _manager = new CustomerManager(
            _mockRepository.Object,
            _mockValidator.Object,
            _mockMessageSession.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task CreateCustomerAsync_ValidData_CreatesCustomerAndPublishesEvent()
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

        _mockValidator.Setup(v => v.ValidateCreateCustomerAsync(email, birthDate, mailingAddress.ZipCode))
            .ReturnsAsync(new ValidationResult { IsValid = true });

        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<Domain.Models.Customer>()))
            .ReturnsAsync((Domain.Models.Customer c) => c);

        // Act
        var result = await _manager.CreateCustomerAsync(firstName, lastName, email, phoneNumber, mailingAddress, birthDate);

        // Assert
        result.Should().NotBeNull();
        result.CustomerId.Should().StartWith("CUST-");
        result.FirstName.Should().Be(firstName);
        result.LastName.Should().Be(lastName);
        result.Email.Should().Be(email);
        result.PhoneNumber.Should().Be(phoneNumber);
        result.Status.Should().Be("Active");

        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<Domain.Models.Customer>()), Times.Once);
        _mockMessageSession.Verify(
            m => m.Publish(It.IsAny<CustomerCreated>(), It.IsAny<PublishOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateCustomerAsync_InvalidEmail_ThrowsValidationException()
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

        _mockValidator.Setup(v => v.ValidateCreateCustomerAsync(email, birthDate, mailingAddress.ZipCode))
            .ReturnsAsync(validationResult);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _manager.CreateCustomerAsync(firstName, lastName, email, phoneNumber, mailingAddress, birthDate));

        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<Domain.Models.Customer>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCustomerAsync_ValidData_UpdatesCustomerAndPublishesEvent()
    {
        // Arrange
        var customerId = Guid.NewGuid().ToString();
        var existingCustomer = new Domain.Models.Customer
        {
            Id = customerId,
            CustomerId = customerId,
            Email = "test@example.com",
            FirstName = "John"
        };

        _mockRepository.Setup(r => r.GetByIdAsync(customerId))
            .ReturnsAsync(existingCustomer);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Domain.Models.Customer>()))
            .ReturnsAsync((Domain.Models.Customer c) => c);

        // Act
        var result = await _manager.UpdateCustomerAsync(
            customerId,
            "Jane",
            "Doe",
            "+1-555-1234",
            null);

        // Assert
        result.Should().NotBeNull();
        result.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Doe");
        result.PhoneNumber.Should().Be("+1-555-1234");

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<Domain.Models.Customer>()), Times.Once);
        _mockMessageSession.Verify(
            m => m.Publish(It.IsAny<CustomerInformationUpdated>(), It.IsAny<PublishOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task CloseCustomerAsync_ExistingCustomer_AnonymizesDataAndPublishesEvent()
    {
        // Arrange
        var customerId = Guid.NewGuid().ToString();
        var existingCustomer = new Domain.Models.Customer
        {
            Id = customerId,
            CustomerId = customerId,
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            PhoneNumber = "+1-555-1234",
            Status = "Active"
        };

        _mockRepository.Setup(r => r.GetByIdAsync(customerId))
            .ReturnsAsync(existingCustomer);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Domain.Models.Customer>()))
            .ReturnsAsync((Domain.Models.Customer c) => c);

        // Act
        await _manager.CloseCustomerAsync(customerId);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<Domain.Models.Customer>(c =>
            c.Status == "Closed" &&
            c.FirstName == null &&
            c.LastName == null &&
            c.PhoneNumber == null &&
            c.Email.Contains("anonymized"))), Times.Once);

        _mockMessageSession.Verify(
            m => m.Publish(It.IsAny<CustomerClosed>(), It.IsAny<PublishOptions>()),
            Times.Once);
    }
}
