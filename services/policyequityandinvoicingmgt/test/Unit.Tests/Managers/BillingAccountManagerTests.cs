namespace RiskInsure.PolicyEquityAndInvoicingMgt.Unit.Tests.Managers;

using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Managers;
using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Managers.DTOs;
using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Models;
using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Services.PolicyEquityAndInvoicingDb;
using Microsoft.Extensions.Logging;
using NServiceBus;

/// <summary>
/// Unit tests for PolicyEquityAndInvoicingAccountManager business logic.
/// Tests account lifecycle operations, validation, and state transitions.
/// </summary>
public class PolicyEquityAndInvoicingAccountManagerTests
{
    private readonly Mock<IPolicyEquityAndInvoicingAccountRepository> _mockRepository;
    private readonly Mock<IMessageSession> _mockMessageSession;
    private readonly Mock<ILogger<PolicyEquityAndInvoicingAccountManager>> _mockLogger;
    private readonly PolicyEquityAndInvoicingAccountManager _manager;

    public PolicyEquityAndInvoicingAccountManagerTests()
    {
        _mockRepository = new Mock<IPolicyEquityAndInvoicingAccountRepository>();
        _mockMessageSession = new Mock<IMessageSession>();
        _mockLogger = new Mock<ILogger<PolicyEquityAndInvoicingAccountManager>>();

        _manager = new PolicyEquityAndInvoicingAccountManager(
            _mockRepository.Object,
            _mockMessageSession.Object,
            _mockLogger.Object);
    }

    #region CreatePolicyEquityAndInvoicingAccountAsync Tests

    [Fact]
    public async Task CreatePolicyEquityAndInvoicingAccountAsync_ValidAccount_ReturnsSuccess()
    {
        // Arrange
        var dto = new CreatePolicyEquityAndInvoicingAccountDto
        {
            AccountId = "test-account-123",
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow.AddDays(-30)
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(dto.AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PolicyEquityAndInvoicingAccount?)null);

        _mockRepository.Setup(r => r.GetByCustomerIdAsync(dto.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PolicyEquityAndInvoicingAccount?)null);

        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<PolicyEquityAndInvoicingAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.CreatePolicyEquityAndInvoicingAccountAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.AccountId.Should().Be(dto.AccountId);

        _mockRepository.Verify(r => r.CreateAsync(
            It.Is<PolicyEquityAndInvoicingAccount>(a =>
                a.AccountId == dto.AccountId &&
                a.Status == PolicyEquityAndInvoicingAccountStatus.Pending &&
                a.TotalPaid == 0),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockMessageSession.Verify(m => m.Publish(
            It.IsAny<object>(),
            It.IsAny<PublishOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task CreatePolicyEquityAndInvoicingAccountAsync_DuplicateAccountId_ReturnsSuccessIdempotent()
    {
        // Arrange
        var dto = new CreatePolicyEquityAndInvoicingAccountDto
        {
            AccountId = "existing-account",
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow
        };

        var existingAccount = new PolicyEquityAndInvoicingAccount
        {
            AccountId = dto.AccountId,
            CustomerId = dto.CustomerId,
            PolicyNumber = dto.PolicyNumber,
            PolicyHolderName = dto.PolicyHolderName,
            CurrentPremiumOwed = dto.CurrentPremiumOwed,
            TotalPaid = 0,
            Status = PolicyEquityAndInvoicingAccountStatus.Pending,
            PolicyEquityAndInvoicingCycle = dto.PolicyEquityAndInvoicingCycle,
            EffectiveDate = dto.EffectiveDate,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(dto.AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAccount);

        // Act
        var result = await _manager.CreatePolicyEquityAndInvoicingAccountAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockRepository.Verify(r => r.CreateAsync(
            It.IsAny<PolicyEquityAndInvoicingAccount>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreatePolicyEquityAndInvoicingAccountAsync_DuplicatePolicyNumber_ReturnsFailure()
    {
        // Arrange
        var dto = new CreatePolicyEquityAndInvoicingAccountDto
        {
            AccountId = "new-account",
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow
        };

        var existingAccount = new PolicyEquityAndInvoicingAccount
        {
            AccountId = "different-account",
            CustomerId = dto.CustomerId,
            PolicyNumber = dto.PolicyNumber,
            PolicyHolderName = "Existing User",
            CurrentPremiumOwed = 300m,
            TotalPaid = 0,
            Status = PolicyEquityAndInvoicingAccountStatus.Active,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(dto.AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PolicyEquityAndInvoicingAccount?)null);

        _mockRepository.Setup(r => r.GetByCustomerIdAsync(dto.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAccount);

        // Act
        var result = await _manager.CreatePolicyEquityAndInvoicingAccountAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("DUPLICATE_POLICY_NUMBER");
        result.ErrorMessage.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreatePolicyEquityAndInvoicingAccountAsync_NegativePremium_ReturnsFailure()
    {
        // Arrange
        var dto = new CreatePolicyEquityAndInvoicingAccountDto
        {
            AccountId = "test-account",
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = -100m,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(dto.AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PolicyEquityAndInvoicingAccount?)null);

        // Act
        var result = await _manager.CreatePolicyEquityAndInvoicingAccountAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NEGATIVE_PREMIUM");
        result.ErrorMessage.Should().Contain("cannot be negative");
    }

    [Fact]
    public async Task CreatePolicyEquityAndInvoicingAccountAsync_EffectiveDateTooOld_ReturnsFailure()
    {
        // Arrange
        var dto = new CreatePolicyEquityAndInvoicingAccountDto
        {
            AccountId = "test-account",
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow.AddDays(-100) // More than 90 days in the past
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(dto.AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PolicyEquityAndInvoicingAccount?)null);

        // Act
        var result = await _manager.CreatePolicyEquityAndInvoicingAccountAsync(dto);

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
        var account = new PolicyEquityAndInvoicingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = PolicyEquityAndInvoicingAccountStatus.Pending,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<PolicyEquityAndInvoicingAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.ActivateAccountAsync(accountId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<PolicyEquityAndInvoicingAccount>(a => a.Status == PolicyEquityAndInvoicingAccountStatus.Active),
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
            .ReturnsAsync((PolicyEquityAndInvoicingAccount?)null);

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
        var account = new PolicyEquityAndInvoicingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = PolicyEquityAndInvoicingAccountStatus.Active,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
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
            It.IsAny<PolicyEquityAndInvoicingAccount>(),
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
        var account = new PolicyEquityAndInvoicingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = PolicyEquityAndInvoicingAccountStatus.Active,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
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

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<PolicyEquityAndInvoicingAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.UpdatePremiumOwedAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<PolicyEquityAndInvoicingAccount>(a => a.CurrentPremiumOwed == 750m),
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
        var account = new PolicyEquityAndInvoicingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = PolicyEquityAndInvoicingAccountStatus.Closed,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
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
        var account = new PolicyEquityAndInvoicingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = PolicyEquityAndInvoicingAccountStatus.Active,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
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
        var account = new PolicyEquityAndInvoicingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = PolicyEquityAndInvoicingAccountStatus.Active,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<PolicyEquityAndInvoicingAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.SuspendAccountAsync(accountId, "Non-payment");

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<PolicyEquityAndInvoicingAccount>(a => a.Status == PolicyEquityAndInvoicingAccountStatus.Suspended),
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
        var account = new PolicyEquityAndInvoicingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = PolicyEquityAndInvoicingAccountStatus.Closed,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
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
        var account = new PolicyEquityAndInvoicingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = PolicyEquityAndInvoicingAccountStatus.Suspended,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
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
            It.IsAny<PolicyEquityAndInvoicingAccount>(),
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
        var account = new PolicyEquityAndInvoicingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 500m,
            Status = PolicyEquityAndInvoicingAccountStatus.Active,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<PolicyEquityAndInvoicingAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.CloseAccountAsync(accountId, "Policy terminated");

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<PolicyEquityAndInvoicingAccount>(a => a.Status == PolicyEquityAndInvoicingAccountStatus.Closed),
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
        var account = new PolicyEquityAndInvoicingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 500m,
            Status = PolicyEquityAndInvoicingAccountStatus.Closed,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
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
            It.IsAny<PolicyEquityAndInvoicingAccount>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region UpdatePolicyEquityAndInvoicingCycleAsync Tests

    [Fact]
    public async Task UpdatePolicyEquityAndInvoicingCycleAsync_ValidUpdate_ReturnsSuccess()
    {
        // Arrange
        var accountId = "test-account";
        var account = new PolicyEquityAndInvoicingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = PolicyEquityAndInvoicingAccountStatus.Active,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        var dto = new UpdatePolicyEquityAndInvoicingCycleDto
        {
            AccountId = accountId,
            NewPolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Quarterly,
            ChangeReason = "Customer request"
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<PolicyEquityAndInvoicingAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.UpdatePolicyEquityAndInvoicingCycleAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<PolicyEquityAndInvoicingAccount>(a => a.PolicyEquityAndInvoicingCycle == PolicyEquityAndInvoicingCycle.Quarterly),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockMessageSession.Verify(m => m.Publish(
            It.IsAny<object>(),
            It.IsAny<PublishOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdatePolicyEquityAndInvoicingCycleAsync_ClosedAccount_ReturnsFailure()
    {
        // Arrange
        var accountId = "test-account";
        var account = new PolicyEquityAndInvoicingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = PolicyEquityAndInvoicingAccountStatus.Closed,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        var dto = new UpdatePolicyEquityAndInvoicingCycleDto
        {
            AccountId = accountId,
            NewPolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Quarterly,
            ChangeReason = "Customer request"
        };

        _mockRepository.Setup(r => r.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act
        var result = await _manager.UpdatePolicyEquityAndInvoicingCycleAsync(dto);

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
        var account = new PolicyEquityAndInvoicingAccount
        {
            AccountId = accountId,
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 0m,
            Status = PolicyEquityAndInvoicingAccountStatus.Active,
            PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
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
            .ReturnsAsync((PolicyEquityAndInvoicingAccount?)null);

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
        var accounts = new List<PolicyEquityAndInvoicingAccount>
        {
            new PolicyEquityAndInvoicingAccount
            {
                AccountId = "account-1",
                CustomerId = "CUST-1",
                PolicyNumber = "POL-1",
                PolicyHolderName = "User 1",
                CurrentPremiumOwed = 500m,
                TotalPaid = 0m,
                Status = PolicyEquityAndInvoicingAccountStatus.Active,
                PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Monthly,
                EffectiveDate = DateTimeOffset.UtcNow,
                CreatedUtc = DateTimeOffset.UtcNow,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            },
            new PolicyEquityAndInvoicingAccount
            {
                AccountId = "account-2",
                CustomerId = "CUST-2",
                PolicyNumber = "POL-2",
                PolicyHolderName = "User 2",
                CurrentPremiumOwed = 750m,
                TotalPaid = 0m,
                Status = PolicyEquityAndInvoicingAccountStatus.Pending,
                PolicyEquityAndInvoicingCycle = PolicyEquityAndInvoicingCycle.Quarterly,
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
