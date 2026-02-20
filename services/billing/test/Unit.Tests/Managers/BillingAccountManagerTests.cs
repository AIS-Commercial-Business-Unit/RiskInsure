namespace RiskInsure.Billing.Unit.Tests.Managers;

using RiskInsure.Billing.Domain.Managers;
using RiskInsure.Billing.Domain.Managers.DTOs;
using RiskInsure.Billing.Domain.Models;
using RiskInsure.Billing.Domain.Services.BillingDb;
using Microsoft.Extensions.Logging;
using NServiceBus;

/// <summary>
/// Unit tests for BillingAccountManager business logic.
/// Tests account lifecycle operations, validation, and state transitions.
/// </summary>
public class BillingAccountManagerTests
{
    private readonly Mock<IBillingAccountRepository> _mockRepository;
    private readonly Mock<IMessageSession> _mockMessageSession;
    private readonly Mock<ILogger<BillingAccountManager>> _mockLogger;
    private readonly BillingAccountManager _manager;

    public BillingAccountManagerTests()
    {
        _mockRepository = new Mock<IBillingAccountRepository>();
        _mockMessageSession = new Mock<IMessageSession>();
        _mockLogger = new Mock<ILogger<BillingAccountManager>>();

        _manager = new BillingAccountManager(
            _mockRepository.Object,
            _mockMessageSession.Object,
            _mockLogger.Object);
    }

    #region CreateBillingAccountAsync Tests

    [Fact]
    public async Task CreateBillingAccountAsync_ValidAccount_ReturnsSuccess()
    {
        // Arrange
        var dto = new CreateBillingAccountDto
        {
            AccountId = "test-account-123",
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow.AddDays(-30)
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(dto.AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingAccount?)null);

        _mockRepository.Setup(r => r.GetByCustomerIdAsync(dto.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingAccount?)null);

        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<BillingAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.CreateBillingAccountAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.AccountId.Should().Be(dto.AccountId);

        _mockRepository.Verify(r => r.CreateAsync(
            It.Is<BillingAccount>(a =>
                a.AccountId == dto.AccountId &&
                a.Status == BillingAccountStatus.Pending &&
                a.TotalPaid == 0),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockMessageSession.Verify(m => m.Publish(
            It.IsAny<object>(),
            It.IsAny<PublishOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateBillingAccountAsync_DuplicateAccountId_ReturnsSuccessIdempotent()
    {
        // Arrange
        var dto = new CreateBillingAccountDto
        {
            AccountId = "existing-account",
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow
        };

        var existingAccount = new BillingAccount
        {
            AccountId = dto.AccountId,
            CustomerId = dto.CustomerId,
            PolicyNumber = dto.PolicyNumber,
            PolicyHolderName = dto.PolicyHolderName,
            CurrentPremiumOwed = dto.CurrentPremiumOwed,
            TotalPaid = 0,
            Status = BillingAccountStatus.Pending,
            BillingCycle = dto.BillingCycle,
            EffectiveDate = dto.EffectiveDate,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(dto.AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAccount);

        // Act
        var result = await _manager.CreateBillingAccountAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockRepository.Verify(r => r.CreateAsync(
            It.IsAny<BillingAccount>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateBillingAccountAsync_DuplicatePolicyNumber_ReturnsFailure()
    {
        // Arrange
        var dto = new CreateBillingAccountDto
        {
            AccountId = "new-account",
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow
        };

        var existingAccount = new BillingAccount
        {
            AccountId = "different-account",
            CustomerId = dto.CustomerId,
            PolicyNumber = dto.PolicyNumber,
            PolicyHolderName = "Existing User",
            CurrentPremiumOwed = 300m,
            TotalPaid = 0,
            Status = BillingAccountStatus.Active,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(dto.AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingAccount?)null);

        _mockRepository.Setup(r => r.GetByCustomerIdAsync(dto.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAccount);

        // Act
        var result = await _manager.CreateBillingAccountAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("DUPLICATE_POLICY_NUMBER");
        result.ErrorMessage.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreateBillingAccountAsync_NegativePremium_ReturnsFailure()
    {
        // Arrange
        var dto = new CreateBillingAccountDto
        {
            AccountId = "test-account",
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = -100m,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(dto.AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingAccount?)null);

        // Act
        var result = await _manager.CreateBillingAccountAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NEGATIVE_PREMIUM");
        result.ErrorMessage.Should().Contain("cannot be negative");
    }

    [Fact]
    public async Task CreateBillingAccountAsync_EffectiveDateTooOld_ReturnsFailure()
    {
        // Arrange
        var dto = new CreateBillingAccountDto
        {
            AccountId = "test-account",
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow.AddDays(-100) // More than 90 days in the past
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(dto.AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingAccount?)null);

        // Act
        var result = await _manager.CreateBillingAccountAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_EFFECTIVE_DATE");
        result.ErrorMessage.Should().Contain("90 days");
    }

    #endregion

    #region ActivateAccountAsync Tests

    [Fact]
    public async Task ActivateAccountAsync_PendingAccount_ReturnsSuccess()
    {
        // Arrange
        var accountId = "test-account";
        var account = new BillingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = BillingAccountStatus.Pending,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<BillingAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.ActivateAccountAsync(accountId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<BillingAccount>(a => a.Status == BillingAccountStatus.Active),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockMessageSession.Verify(m => m.Publish(
            It.IsAny<object>(),
            It.IsAny<PublishOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task ActivateAccountAsync_AccountNotFound_ReturnsFailure()
    {
        // Arrange
        var accountId = "nonexistent-account";

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingAccount?)null);

        // Act
        var result = await _manager.ActivateAccountAsync(accountId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCOUNT_NOT_FOUND");
    }

    [Fact]
    public async Task ActivateAccountAsync_AlreadyActive_ReturnsSuccessIdempotent()
    {
        // Arrange
        var accountId = "test-account";
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

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act
        var result = await _manager.ActivateAccountAsync(accountId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockRepository.Verify(r => r.UpdateAsync(
            It.IsAny<BillingAccount>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region UpdatePremiumOwedAsync Tests

    [Fact]
    public async Task UpdatePremiumOwedAsync_ValidUpdate_ReturnsSuccess()
    {
        // Arrange
        var accountId = "test-account";
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

        var dto = new UpdatePremiumOwedDto
        {
            AccountId = accountId,
            NewPremiumOwed = 750m,
            ChangeReason = "Policy coverage increased"
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<BillingAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.UpdatePremiumOwedAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<BillingAccount>(a => a.CurrentPremiumOwed == 750m),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockMessageSession.Verify(m => m.Publish(
            It.IsAny<object>(),
            It.IsAny<PublishOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdatePremiumOwedAsync_ClosedAccount_ReturnsFailure()
    {
        // Arrange
        var accountId = "test-account";
        var account = new BillingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = BillingAccountStatus.Closed,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        var dto = new UpdatePremiumOwedDto
        {
            AccountId = accountId,
            NewPremiumOwed = 750m,
            ChangeReason = "Policy coverage increased"
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act
        var result = await _manager.UpdatePremiumOwedAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCOUNT_CLOSED");
    }

    [Fact]
    public async Task UpdatePremiumOwedAsync_NegativePremium_ReturnsFailure()
    {
        // Arrange
        var accountId = "test-account";
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

        var dto = new UpdatePremiumOwedDto
        {
            AccountId = accountId,
            NewPremiumOwed = -100m,
            ChangeReason = "Invalid update"
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act
        var result = await _manager.UpdatePremiumOwedAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NEGATIVE_PREMIUM");
    }

    #endregion

    #region SuspendAccountAsync Tests

    [Fact]
    public async Task SuspendAccountAsync_ActiveAccount_ReturnsSuccess()
    {
        // Arrange
        var accountId = "test-account";
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

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<BillingAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.SuspendAccountAsync(accountId, "Non-payment");

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<BillingAccount>(a => a.Status == BillingAccountStatus.Suspended),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockMessageSession.Verify(m => m.Publish(
            It.IsAny<object>(),
            It.IsAny<PublishOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task SuspendAccountAsync_ClosedAccount_ReturnsFailure()
    {
        // Arrange
        var accountId = "test-account";
        var account = new BillingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = BillingAccountStatus.Closed,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act
        var result = await _manager.SuspendAccountAsync(accountId, "Non-payment");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCOUNT_CLOSED");
    }

    [Fact]
    public async Task SuspendAccountAsync_AlreadySuspended_ReturnsSuccessIdempotent()
    {
        // Arrange
        var accountId = "test-account";
        var account = new BillingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = BillingAccountStatus.Suspended,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act
        var result = await _manager.SuspendAccountAsync(accountId, "Non-payment");

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockRepository.Verify(r => r.UpdateAsync(
            It.IsAny<BillingAccount>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region CloseAccountAsync Tests

    [Fact]
    public async Task CloseAccountAsync_ActiveAccount_ReturnsSuccess()
    {
        // Arrange
        var accountId = "test-account";
        var account = new BillingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 500m,
            Status = BillingAccountStatus.Active,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<BillingAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.CloseAccountAsync(accountId, "Policy terminated");

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<BillingAccount>(a => a.Status == BillingAccountStatus.Closed),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockMessageSession.Verify(m => m.Publish(
            It.IsAny<object>(),
            It.IsAny<PublishOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task CloseAccountAsync_AlreadyClosed_ReturnsSuccessIdempotent()
    {
        // Arrange
        var accountId = "test-account";
        var account = new BillingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 500m,
            Status = BillingAccountStatus.Closed,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act
        var result = await _manager.CloseAccountAsync(accountId, "Policy terminated");

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockRepository.Verify(r => r.UpdateAsync(
            It.IsAny<BillingAccount>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region UpdateBillingCycleAsync Tests

    [Fact]
    public async Task UpdateBillingCycleAsync_ValidUpdate_ReturnsSuccess()
    {
        // Arrange
        var accountId = "test-account";
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

        var dto = new UpdateBillingCycleDto
        {
            AccountId = accountId,
            NewBillingCycle = BillingCycle.Quarterly,
            ChangeReason = "Customer request"
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<BillingAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.UpdateBillingCycleAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<BillingAccount>(a => a.BillingCycle == BillingCycle.Quarterly),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockMessageSession.Verify(m => m.Publish(
            It.IsAny<object>(),
            It.IsAny<PublishOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateBillingCycleAsync_ClosedAccount_ReturnsFailure()
    {
        // Arrange
        var accountId = "test-account";
        var account = new BillingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = BillingAccountStatus.Closed,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        var dto = new UpdateBillingCycleDto
        {
            AccountId = accountId,
            NewBillingCycle = BillingCycle.Quarterly,
            ChangeReason = "Customer request"
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act
        var result = await _manager.UpdateBillingCycleAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCOUNT_CLOSED");
    }

    #endregion

    #region GetAccountByIdAsync Tests

    [Fact]
    public async Task GetAccountByIdAsync_ExistingAccount_ReturnsAccount()
    {
        // Arrange
        var accountId = "test-account";
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

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act
        var result = await _manager.GetAccountByIdAsync(accountId);

        // Assert
        result.Should().NotBeNull();
        result!.AccountId.Should().Be(accountId);
    }

    [Fact]
    public async Task GetAccountByIdAsync_NonexistentAccount_ReturnsNull()
    {
        // Arrange
        var accountId = "nonexistent-account";

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingAccount?)null);

        // Act
        var result = await _manager.GetAccountByIdAsync(accountId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllAccountsAsync Tests

    [Fact]
    public async Task GetAllAccountsAsync_ReturnsAllAccounts()
    {
        // Arrange
        var accounts = new List<BillingAccount>
        {
            new BillingAccount
            {
                AccountId = "account-1",
                CustomerId = "CUST-1",
                PolicyNumber = "POL-1",
                PolicyHolderName = "User 1",
                CurrentPremiumOwed = 500m,
                TotalPaid = 0m,
                Status = BillingAccountStatus.Active,
                BillingCycle = BillingCycle.Monthly,
                EffectiveDate = DateTimeOffset.UtcNow,
                CreatedUtc = DateTimeOffset.UtcNow,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            },
            new BillingAccount
            {
                AccountId = "account-2",
                CustomerId = "CUST-2",
                PolicyNumber = "POL-2",
                PolicyHolderName = "User 2",
                CurrentPremiumOwed = 750m,
                TotalPaid = 0m,
                Status = BillingAccountStatus.Pending,
                BillingCycle = BillingCycle.Quarterly,
                EffectiveDate = DateTimeOffset.UtcNow,
                CreatedUtc = DateTimeOffset.UtcNow,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(accounts);

        // Act
        var result = await _manager.GetAllAccountsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(a => a.AccountId == "account-1");
        result.Should().Contain(a => a.AccountId == "account-2");
    }

    #endregion
}
