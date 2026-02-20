namespace RiskInsure.Billing.Unit.Tests.Handlers;

using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.Billing.Domain.Contracts.Commands;
using RiskInsure.Billing.Domain.Managers;
using RiskInsure.Billing.Domain.Managers.DTOs;
using RiskInsure.Billing.Domain.Models;
using RiskInsure.Billing.Endpoint.In.Handlers;

/// <summary>
/// Unit tests for RecordPaymentHandler message handler.
/// Tests protocol translation and error handling per constitutional Principle VII.
/// </summary>
public class RecordPaymentHandlerTests
{
    private readonly Mock<IBillingPaymentManager> _mockManager;
    private readonly Mock<ILogger<RecordPaymentHandler>> _mockLogger;
    private readonly Mock<IMessageHandlerContext> _mockContext;
    private readonly RecordPaymentHandler _handler;

    public RecordPaymentHandlerTests()
    {
        _mockManager = new Mock<IBillingPaymentManager>();
        _mockLogger = new Mock<ILogger<RecordPaymentHandler>>();
        _mockContext = new Mock<IMessageHandlerContext>();
        _mockContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        _handler = new RecordPaymentHandler(_mockManager.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_SuccessfulPayment_ProcessesWithoutError()
    {
        // Arrange
        var message = new RecordPayment(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            AccountId: "test-account",
            Amount: 100m,
            ReferenceNumber: "PAY-123",
            IdempotencyKey: "key-123"
        );

        var updatedAccount = new BillingAccount
        {
            AccountId = "test-account",
            CustomerId = "CUST-123",
            PolicyNumber = "POL-123",
            PolicyHolderName = "Test User",
            CurrentPremiumOwed = 500m,
            TotalPaid = 100m,
            Status = BillingAccountStatus.Active,
            BillingCycle = BillingCycle.Monthly,
            EffectiveDate = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        var successResult = new PaymentRecordingResult
        {
            IsSuccess = true,
            WasDuplicate = false,
            UpdatedAccount = updatedAccount
        };

        _mockManager.Setup(m => m.RecordPaymentAsync(
            It.IsAny<RecordPaymentDto>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);

        // Act
        await _handler.Handle(message, _mockContext.Object);

        // Assert
        _mockManager.Verify(m => m.RecordPaymentAsync(
            It.Is<RecordPaymentDto>(dto =>
                dto.AccountId == message.AccountId &&
                dto.Amount == message.Amount &&
                dto.ReferenceNumber == message.ReferenceNumber &&
                dto.IdempotencyKey == message.IdempotencyKey),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicatePayment_LogsAndProcessesIdempotently()
    {
        // Arrange
        var message = new RecordPayment(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            AccountId: "test-account",
            Amount: 100m,
            ReferenceNumber: "PAY-DUPLICATE",
            IdempotencyKey: "key-123"
        );

        var duplicateResult = new PaymentRecordingResult
        {
            IsSuccess = true,
            WasDuplicate = true,
            UpdatedAccount = null
        };

        _mockManager.Setup(m => m.RecordPaymentAsync(
            It.IsAny<RecordPaymentDto>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(duplicateResult);

        // Act
        await _handler.Handle(message, _mockContext.Object);

        // Assert - Should complete without error
        _mockManager.Verify(m => m.RecordPaymentAsync(
            It.IsAny<RecordPaymentDto>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_RetryableFailure_ThrowsException()
    {
        // Arrange
        var message = new RecordPayment(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            AccountId: "test-account",
            Amount: 100m,
            ReferenceNumber: "PAY-123",
            IdempotencyKey: "key-123"
        );

        var failureResult = new PaymentRecordingResult
        {
            IsSuccess = false,
            IsRetryable = true,
            ErrorCode = "TEMPORARY_ERROR",
            ErrorMessage = "Database timeout"
        };

        _mockManager.Setup(m => m.RecordPaymentAsync(
            It.IsAny<RecordPaymentDto>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(failureResult);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(message, _mockContext.Object));

        exception.Message.Should().Contain("Failed to record payment");
    }

    [Fact]
    public async Task Handle_NonRetryableFailure_ThrowsException()
    {
        // Arrange
        var message = new RecordPayment(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            AccountId: "test-account",
            Amount: -50m, // Invalid amount
            ReferenceNumber: "PAY-123",
            IdempotencyKey: "key-123"
        );

        var failureResult = new PaymentRecordingResult
        {
            IsSuccess = false,
            IsRetryable = false,
            ErrorCode = "INVALID_AMOUNT",
            ErrorMessage = "Amount must be greater than zero"
        };

        _mockManager.Setup(m => m.RecordPaymentAsync(
            It.IsAny<RecordPaymentDto>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(failureResult);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(message, _mockContext.Object));

        exception.Message.Should().Contain("Business rule violation");
    }
}
