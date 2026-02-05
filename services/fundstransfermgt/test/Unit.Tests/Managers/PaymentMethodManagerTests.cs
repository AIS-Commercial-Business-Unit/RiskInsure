using RiskInsure.FundTransferMgt.Domain.Managers;
using RiskInsure.FundTransferMgt.Domain.Models;
using RiskInsure.FundTransferMgt.Domain.Repositories;
using RiskInsure.FundTransferMgt.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace RiskInsure.FundTransferMgt.Unit.Tests.Managers;

public class PaymentMethodManagerTests
{
    private readonly Mock<IPaymentMethodRepository> _mockRepository;
    private readonly Mock<IPaymentGateway> _mockGateway;
    private readonly Mock<ILogger<PaymentMethodManager>> _mockLogger;
    private readonly PaymentMethodManager _manager;

    public PaymentMethodManagerTests()
    {
        _mockRepository = new Mock<IPaymentMethodRepository>();
        _mockGateway = new Mock<IPaymentGateway>();
        _mockLogger = new Mock<ILogger<PaymentMethodManager>>();
        _manager = new PaymentMethodManager(_mockRepository.Object, _mockGateway.Object, _mockLogger.Object);
    }

    #region Credit Card Tests

    [Theory]
    [InlineData("4532015112830366", "Visa")] // Valid Visa
    [InlineData("5425233430109903", "MasterCard")] // Valid MasterCard
    [InlineData("374245455400126", "AmericanExpress")] // Valid Amex
    [InlineData("6011000991001201", "Discover")] // Valid Discover
    public async Task AddCreditCardAsync_ValidCard_CreatesPaymentMethod(string cardNumber, string expectedBrand)
    {
        // Arrange
        var customerId = "CUST-123";
        var cardholderName = "John Doe";
        var expiryMonth = 12;
        var expiryYear = 2027;
        var cvv = "123";
        var billingAddress = new Address
        {
            Street = "123 Main St",
            City = "Anytown",
            State = "CA",
            PostalCode = "12345",
            Country = "US"
        };

        _mockGateway.Setup(g => g.TokenizeCardAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("tok_123");

        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentMethod pm, CancellationToken ct) => pm);

        // Act
        var result = await _manager.AddCreditCardAsync(
            customerId, cardholderName, cardNumber, expiryMonth, expiryYear, cvv, billingAddress);

        // Assert
        result.Should().NotBeNull();
        result.CustomerId.Should().Be(customerId);
        result.Type.Should().Be(PaymentMethodType.CreditCard);
        result.Status.Should().Be(PaymentMethodStatus.Validated);
        result.Card.Should().NotBeNull();
        result.Card!.Brand.Should().Be(expectedBrand);
        result.Card.Last4.Should().Be(cardNumber[^4..]);
        result.Card.ExpirationMonth.Should().Be(expiryMonth);
        result.Card.ExpirationYear.Should().Be(expiryYear);

        _mockRepository.Verify(r => r.CreateAsync(It.Is<PaymentMethod>(pm =>
            pm.CustomerId == customerId &&
            pm.Type == PaymentMethodType.CreditCard &&
            pm.Card!.Brand == expectedBrand
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("4532015112830367")] // Invalid Luhn checksum
    [InlineData("1234567890123456")] // Invalid card number
    public async Task AddCreditCardAsync_InvalidLuhnChecksum_ThrowsArgumentException(string cardNumber)
    {
        // Arrange
        var customerId = "CUST-123";
        var cardholderName = "John Doe";
        var expiryMonth = 12;
        var expiryYear = 2027;
        var cvv = "123";
        var billingAddress = new Address { Street = "123 Main St", City = "Anytown", State = "CA", PostalCode = "12345", Country = "US" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _manager.AddCreditCardAsync(customerId, cardholderName, cardNumber, expiryMonth, expiryYear, cvv, billingAddress));

        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0, 2027)] // Invalid month (too low)
    [InlineData(13, 2027)] // Invalid month (too high)
    [InlineData(12, 2024)] // Expired year
    public async Task AddCreditCardAsync_InvalidExpiry_ThrowsArgumentException(int expiryMonth, int expiryYear)
    {
        // Arrange
        var customerId = "CUST-123";
        var cardNumber = "4532015112830366"; // Valid Visa
        var cardholderName = "John Doe";
        var cvv = "123";
        var billingAddress = new Address { Street = "123 Main St", City = "Anytown", State = "CA", PostalCode = "12345", Country = "US" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _manager.AddCreditCardAsync(customerId, cardholderName, cardNumber, expiryMonth, expiryYear, cvv, billingAddress));

        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddCreditCardAsync_EmptyCardholderName_ThrowsArgumentException()
    {
        // Arrange
        var customerId = "CUST-123";
        var cardNumber = "4532015112830366"; // Valid Visa
        var cardholderName = "";
        var expiryMonth = 12;
        var expiryYear = 2027;
        var cvv = "123";
        var billingAddress = new Address { Street = "123 Main St", City = "Anytown", State = "CA", PostalCode = "12345", Country = "US" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _manager.AddCreditCardAsync(customerId, cardholderName, cardNumber, expiryMonth, expiryYear, cvv, billingAddress));

        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region ACH Tests

    [Theory]
    [InlineData("011000015")] // Valid ABA routing number
    [InlineData("122105155")] // Valid ABA routing number
    public async Task AddAchAccountAsync_ValidRoutingNumber_CreatesPaymentMethod(string routingNumber)
    {
        // Arrange
        var customerId = "CUST-123";
        var accountNumber = "123456789";
        var accountHolderName = "Jane Smith";
        var accountType = "Checking";

        _mockGateway.Setup(g => g.TokenizeAchAccountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ach_tok_123");

        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentMethod pm, CancellationToken ct) => pm);

        // Act
        var result = await _manager.AddAchAccountAsync(
            customerId, accountHolderName, routingNumber, accountNumber, accountType);

        // Assert
        result.Should().NotBeNull();
        result.CustomerId.Should().Be(customerId);
        result.Type.Should().Be(PaymentMethodType.Ach);
        result.Status.Should().Be(PaymentMethodStatus.Validated);
        result.Ach.Should().NotBeNull();
        result.Ach!.RoutingNumber.Should().Be(routingNumber);
        result.Ach.Last4.Should().Be(accountNumber[^4..]);

        _mockRepository.Verify(r => r.CreateAsync(It.Is<PaymentMethod>(pm =>
            pm.CustomerId == customerId &&
            pm.Type == PaymentMethodType.Ach &&
            pm.Ach!.RoutingNumber == routingNumber
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("123456789")] // Invalid ABA routing number (checksum)
    [InlineData("00000000")] // Invalid ABA routing number (length)
    public async Task AddAchAccountAsync_InvalidRoutingNumber_ThrowsArgumentException(string routingNumber)
    {
        // Arrange
        var customerId = "CUST-123";
        var accountNumber = "123456789";
        var accountHolderName = "Jane Smith";
        var accountType = "Checking";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _manager.AddAchAccountAsync(customerId, accountHolderName, routingNumber, accountNumber, accountType));

        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddAchAccountAsync_EmptyAccountNumber_ThrowsArgumentException()
    {
        // Arrange
        var customerId = "CUST-123";
        var routingNumber = "011000015"; // Valid ABA routing number
        var accountNumber = "";
        var accountHolderName = "Jane Smith";
        var accountType = "Checking";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _manager.AddAchAccountAsync(customerId, accountHolderName, routingNumber, accountNumber, accountType));

        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetCustomerPaymentMethodsAsync_ReturnsAllCustomerMethods()
    {
        // Arrange
        var customerId = "CUST-123";
        var expectedMethods = new List<PaymentMethod>
        {
            new()
            {
                PaymentMethodId = "PM-001",
                CustomerId = customerId,
                Type = PaymentMethodType.CreditCard,
                Status = PaymentMethodStatus.Active
            },
            new()
            {
                PaymentMethodId = "PM-002",
                CustomerId = customerId,
                Type = PaymentMethodType.Ach,
                Status = PaymentMethodStatus.Active
            }
        };

        _mockRepository.Setup(r => r.GetByCustomerIdAsync(customerId))
            .ReturnsAsync(expectedMethods);

        // Act
        var result = await _manager.GetCustomerPaymentMethodsAsync(customerId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(expectedMethods);
    }

    #endregion

    #region Removal Tests

    [Fact]
    public async Task RemovePaymentMethodAsync_ExistingMethod_MarksAsInactive()
    {
        // Arrange
        var paymentMethodId = "PM-001";
        var existingMethod = new PaymentMethod
        {
            PaymentMethodId = paymentMethodId,
            CustomerId = "CUST-123",
            Type = PaymentMethodType.CreditCard,
            Status = PaymentMethodStatus.Active
        };

        _mockRepository.Setup(r => r.GetByIdAsync(paymentMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMethod);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentMethod pm, CancellationToken ct) => pm);

        // Act
        await _manager.RemovePaymentMethodAsync(paymentMethodId);

        // Assert
        existingMethod.Status.Should().Be(PaymentMethodStatus.Inactive);

        _mockRepository.Verify(r => r.UpdateAsync(It.Is<PaymentMethod>(pm =>
            pm.PaymentMethodId == paymentMethodId &&
            pm.Status == PaymentMethodStatus.Inactive
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemovePaymentMethodAsync_NonExistentMethod_ThrowsInvalidOperationException()
    {
        // Arrange
        var paymentMethodId = "PM-NONEXISTENT";

        _mockRepository.Setup(r => r.GetByIdAsync(paymentMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentMethod?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.RemovePaymentMethodAsync(paymentMethodId));

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}

