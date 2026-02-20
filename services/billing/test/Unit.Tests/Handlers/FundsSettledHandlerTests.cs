namespace RiskInsure.Billing.Unit.Tests.Handlers;

using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.Billing.Domain.Managers;
using RiskInsure.Billing.Endpoint.In.Handlers;
using RiskInsure.PublicContracts.Events;

/// <summary>
/// Unit tests for FundsSettledHandler message handler.
/// Tests handling of settled fund events from payment processing.
/// </summary>
public class FundsSettledHandlerTests
{
    private readonly Mock<IBillingPaymentManager> _mockManager;
    private readonly Mock<ILogger<FundsSettledHandler>> _mockLogger;
    private readonly Mock<IMessageHandlerContext> _mockContext;
    private readonly FundsSettledHandler _handler;

    public FundsSettledHandlerTests()
    {
        _mockManager = new Mock<IBillingPaymentManager>();
        _mockLogger = new Mock<ILogger<FundsSettledHandler>>();
        _mockContext = new Mock<IMessageHandlerContext>();
        _mockContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        _handler = new FundsSettledHandler(_mockManager.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidFundsSettled_CallsApplySettledFunds()
    {
        // Arrange
        var message = new FundsSettled(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            CustomerId: "CUST-123",
            TransactionId: "TXN-456",
            Amount: 250m,
            PaymentMethodId: "PM-789",
            SettledUtc: DateTimeOffset.UtcNow,
            IdempotencyKey: "settled-key-123"
        );

        _mockManager.Setup(m => m.ApplySettledFundsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<decimal>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(message, _mockContext.Object);

        // Assert
        _mockManager.Verify(m => m.ApplySettledFundsAsync(
            message.CustomerId,
            message.TransactionId,
            message.Amount,
            message.SettledUtc,
            message.IdempotencyKey,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ManagerThrowsException_RethrowsForRetry()
    {
        // Arrange
        var message = new FundsSettled(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            CustomerId: "CUST-123",
            TransactionId: "TXN-456",
            Amount: 250m,
            PaymentMethodId: "PM-789",
            SettledUtc: DateTimeOffset.UtcNow,
            IdempotencyKey: "settled-key-123"
        );

        _mockManager.Setup(m => m.ApplySettledFundsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<decimal>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(message, _mockContext.Object));

        _mockManager.Verify(m => m.ApplySettledFundsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<decimal>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
