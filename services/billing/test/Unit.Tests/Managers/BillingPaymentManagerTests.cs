namespace RiskInsure.Billing.Unit.Tests.Managers;

using RiskInsure.Billing.Domain.Managers;
using RiskInsure.Billing.Domain.Managers.DTOs;
using RiskInsure.Billing.Domain.Models;
using RiskInsure.Billing.Domain.Services.BillingDb;
using Microsoft.Extensions.Logging;
using NServiceBus;

/// <summary>
/// Unit tests for BillingPaymentManager business logic.
/// Tests business rules, validation, and orchestration without external dependencies.
/// </summary>
public class BillingPaymentManagerTests
{
    private readonly Mock<IBillingAccountRepository> _mockRepository;
    private readonly Mock<IMessageSession> _mockMessageSession;
    private readonly Mock<ILogger<BillingPaymentManager>> _mockLogger;
    private readonly BillingPaymentManager _manager;

    public BillingPaymentManagerTests()
    {
        _mockRepository = new Mock<IBillingAccountRepository>();
        _mockMessageSession = new Mock<IMessageSession>();
        _mockLogger = new Mock<ILogger<BillingPaymentManager>>();
        
        _manager = new BillingPaymentManager(
            _mockRepository.Object,
            _mockMessageSession.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task RecordPaymentAsync_ValidPayment_ReturnsSuccess()
    {
        // Arrange
        var accountId = "test-account-123";
        var account = new BillingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = BillingAccountStatus.Active,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        var dto = new RecordPaymentDto
        {
            AccountId = accountId,
            Amount = 150m,
            ReferenceNumber = "PAY-001",
            OccurredUtc = DateTimeOffset.UtcNow,
            IdempotencyKey = "key-001"
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<BillingAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.RecordPaymentAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.UpdatedAccount.Should().NotBeNull();
        result.UpdatedAccount!.TotalPaid.Should().Be(150m);
        result.UpdatedAccount.OutstandingBalance.Should().Be(350m);

        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<BillingAccount>(a => a.TotalPaid == 150m), 
            It.IsAny<CancellationToken>()), 
            Times.Once);

        _mockMessageSession.Verify(m => m.Publish(
            It.IsAny<object>(), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task RecordPaymentAsync_NegativeAmount_ReturnsFailure()
    {
        // Arrange
        var dto = new RecordPaymentDto
        {
            AccountId = "test-account",
            Amount = -50m,
            ReferenceNumber = "PAY-002",
            OccurredUtc = DateTimeOffset.UtcNow,
            IdempotencyKey = "key-002"
        };

        // Act
        var result = await _manager.RecordPaymentAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_AMOUNT");
        result.ErrorMessage.Should().Contain("greater than zero");

        _mockRepository.Verify(r => r.GetByAccountIdAsync(
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Fact]
    public async Task RecordPaymentAsync_BelowMinimum_ReturnsFailure()
    {
        // Arrange
        var dto = new RecordPaymentDto
        {
            AccountId = "test-account",
            Amount = 0.50m, // Below $1.00 minimum
            ReferenceNumber = "PAY-003",
            OccurredUtc = DateTimeOffset.UtcNow,
            IdempotencyKey = "key-003"
        };

        // Act
        var result = await _manager.RecordPaymentAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("AMOUNT_BELOW_MINIMUM");
        result.ErrorMessage.Should().Contain("at least $1.00");
    }

    [Fact]
    public async Task RecordPaymentAsync_AccountNotFound_ReturnsFailure()
    {
        // Arrange
        var dto = new RecordPaymentDto
        {
            AccountId = "nonexistent-account",
            Amount = 100m,
            ReferenceNumber = "PAY-004",
            OccurredUtc = DateTimeOffset.UtcNow,
            IdempotencyKey = "key-004"
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync("nonexistent-account", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingAccount?)null);

        // Act
        var result = await _manager.RecordPaymentAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCOUNT_NOT_FOUND");
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task RecordPaymentAsync_InactiveAccount_ReturnsFailure()
    {
        // Arrange
        var account = new BillingAccount
        {
            AccountId = "test-account",
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = BillingAccountStatus.Suspended, // Not Active
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        var dto = new RecordPaymentDto
        {
            AccountId = "test-account",
            Amount = 100m,
            ReferenceNumber = "PAY-005",
            OccurredUtc = DateTimeOffset.UtcNow,
            IdempotencyKey = "key-005"
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync("test-account", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act
        var result = await _manager.RecordPaymentAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_ACCOUNT_STATUS");
        result.ErrorMessage.Should().Contain("Suspended");
    }

    [Fact]
    public async Task RecordPaymentAsync_ExceedsBalance_ReturnsFailure()
    {
        // Arrange
        var account = new BillingAccount
        {
            AccountId = "test-account",
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 100m,
            TotalPaid = 50m, // Outstanding = $50
            Status = BillingAccountStatus.Active,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        var dto = new RecordPaymentDto
        {
            AccountId = "test-account",
            Amount = 75m, // Exceeds $50 outstanding
            ReferenceNumber = "PAY-006",
            OccurredUtc = DateTimeOffset.UtcNow,
            IdempotencyKey = "key-006"
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync("test-account", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act
        var result = await _manager.RecordPaymentAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYMENT_EXCEEDS_BALANCE");
        result.ErrorMessage.Should().Contain("exceeds outstanding balance");
    }
}
