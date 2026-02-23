namespace RiskInsure.Billing.Unit.Tests.Handlers;

using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.Billing.Domain.Managers;
using RiskInsure.Billing.Endpoint.In.Handlers;
using RiskInsure.PublicContracts.Events;

/// <summary>
/// Unit tests for FundsRefundedHandler message handler.
/// Tests handling of refund events from payment processing.
/// </summary>
public class FundsRefundedHandlerTests
{
    private readonly Mock<IBillingPaymentManager> _mockManager;
    private readonly Mock<ILogger<FundsRefundedHandler>> _mockLogger;
    private readonly Mock<IMessageHandlerContext> _mockContext;
    private readonly FundsRefundedHandler _handler;

    public FundsRefundedHandlerTests()
    {
        _mockManager = new Mock<IBillingPaymentManager>();
        _mockLogger = new Mock<ILogger<FundsRefundedHandler>>();
        _mockContext = new Mock<IMessageHandlerContext>();
        _mockContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        _handler = new FundsRefundedHandler(_mockManager.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidRefund_CallsApplyRefund()
    {
        // Arrange
        var message = new FundsRefunded(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            CustomerId: "CUST-123",
            RefundId: "REF-789",
            OriginalTransactionId: "TXN-456",
            Amount: 50m,
            RefundedUtc: DateTimeOffset.UtcNow,
            Reason: "Customer request",
            IdempotencyKey: "refund-key-123"
        );

        _mockManager.Setup(m => m.ApplyRefundAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<decimal>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(message, _mockContext.Object);

        // Assert
        _mockManager.Verify(m => m.ApplyRefundAsync(
            message.CustomerId,
            message.RefundId,
            message.OriginalTransactionId,
            message.Amount,
            message.RefundedUtc,
            message.Reason,
            message.IdempotencyKey,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ManagerThrowsException_RethrowsForRetry()
    {
        // Arrange
        var message = new FundsRefunded(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            CustomerId: "CUST-123",
            RefundId: "REF-789",
            OriginalTransactionId: "TXN-456",
            Amount: 50m,
            RefundedUtc: DateTimeOffset.UtcNow,
            Reason: "Customer request",
            IdempotencyKey: "refund-key-123"
        );

        _mockManager.Setup(m => m.ApplyRefundAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<decimal>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Account not found"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(message, _mockContext.Object));

        _mockManager.Verify(m => m.ApplyRefundAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<decimal>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
