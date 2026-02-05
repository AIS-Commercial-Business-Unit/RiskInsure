using RiskInsure.FundTransferMgt.Domain.Managers;
using RiskInsure.FundTransferMgt.Domain.Models;
using RiskInsure.FundTransferMgt.Domain.Repositories;
using RiskInsure.FundTransferMgt.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NServiceBus;
using RiskInsure.PublicContracts.Events;
using Xunit;

namespace RiskInsure.FundTransferMgt.Unit.Tests.Managers;

public class FundTransferManagerTests
{
    private readonly Mock<ITransactionRepository> _mockTransactionRepo;
    private readonly Mock<IPaymentMethodRepository> _mockPaymentMethodRepo;
    private readonly Mock<IPaymentGateway> _mockGateway;
    private readonly Mock<IMessageSession> _mockMessageSession;
    private readonly Mock<ILogger<FundTransferManager>> _mockLogger;
    private readonly FundTransferManager _manager;

    public FundTransferManagerTests()
    {
        _mockTransactionRepo = new Mock<ITransactionRepository>();
        _mockPaymentMethodRepo = new Mock<IPaymentMethodRepository>();
        _mockGateway = new Mock<IPaymentGateway>();
        _mockMessageSession = new Mock<IMessageSession>();
        _mockLogger = new Mock<ILogger<FundTransferManager>>();

        _manager = new FundTransferManager(
            _mockTransactionRepo.Object,
            _mockPaymentMethodRepo.Object,
            _mockGateway.Object,
            _mockMessageSession.Object,
            _mockLogger.Object);
    }

    #region InitiateTransferAsync Tests

    [Fact]
    public async Task InitiateTransferAsync_ValidTransfer_ProcessesSuccessfully()
    {
        // Arrange
        var customerId = "CUST-123";
        var paymentMethodId = "PM-001";
        var amount = 100.00m;
        var purpose = "Premium payment";

        var paymentMethod = new PaymentMethod
        {
            PaymentMethodId = paymentMethodId,
            CustomerId = customerId,
            Type = PaymentMethodType.CreditCard,
            Status = PaymentMethodStatus.Active,
            Card = new CardDetails
            {
                Token = "tok_123",
                Brand = "Visa",
                Last4 = "1234",
                ExpirationMonth = 12,
                ExpirationYear = 2025
            }
        };

        _mockPaymentMethodRepo.Setup(r => r.GetByIdAsync(paymentMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paymentMethod);

        var authResult = new AuthorizationResult
        {
            IsSuccess = true,
            GatewayTransactionId = "auth-123",
            ProcessedUtc = DateTimeOffset.UtcNow
        };

        _mockGateway.Setup(g => g.AuthorizeCardPaymentAsync(
            It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        _mockTransactionRepo.Setup(r => r.CreateTransferAsync(It.IsAny<FundTransfer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FundTransfer ft, CancellationToken ct) => ft);
        
        _mockTransactionRepo.Setup(r => r.UpdateTransferAsync(It.IsAny<FundTransfer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FundTransfer ft, CancellationToken ct) => ft);

        _mockMessageSession.Setup(s => s.Publish(It.IsAny<FundsSettled>(), It.IsAny<PublishOptions>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.InitiateTransferAsync(
            customerId, paymentMethodId, amount, purpose);

        // Assert
        result.Should().NotBeNull();
        result.CustomerId.Should().Be(customerId);
        result.PaymentMethodId.Should().Be(paymentMethodId);
        result.Amount.Should().Be(amount);
        result.Direction.Should().Be(TransferDirection.Inbound);
        result.Status.Should().Be(TransferStatus.Settled);
        result.GatewayTransactionId.Should().Be("auth-123");
        result.SettledUtc.Should().NotBeNull();

        _mockTransactionRepo.Verify(r => r.UpdateTransferAsync(It.Is<FundTransfer>(ft =>
            ft.CustomerId == customerId &&
            ft.PaymentMethodId == paymentMethodId &&
            ft.Amount == amount &&
            ft.Status == TransferStatus.Settled
        ), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        _mockMessageSession.Verify(s => s.Publish(It.Is<FundsSettled>(e =>
            e.CustomerId == customerId &&
            e.Amount == amount &&
            e.PaymentMethodId == paymentMethodId
        ), It.IsAny<PublishOptions>()), Times.Once);
    }

    [Fact]
    public async Task InitiateTransferAsync_InvalidPaymentMethod_ThrowsInvalidOperationException()
    {
        // Arrange
        var customerId = "CUST-123";
        var paymentMethodId = "PM-INVALID";
        var amount = 100.00m;
        var purpose = "Premium payment";

        _mockPaymentMethodRepo.Setup(r => r.GetByIdAsync(paymentMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentMethod?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.InitiateTransferAsync(customerId, paymentMethodId, amount, purpose));

        _mockTransactionRepo.Verify(r => r.CreateTransferAsync(It.IsAny<FundTransfer>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockMessageSession.Verify(s => s.Publish(It.IsAny<FundsSettled>(), It.IsAny<PublishOptions>()), Times.Never);
    }

    [Fact]
    public async Task InitiateTransferAsync_InactivePaymentMethod_ThrowsInvalidOperationException()
    {
        // Arrange
        var customerId = "CUST-123";
        var paymentMethodId = "PM-001";
        var amount = 100.00m;
        var purpose = "Premium payment";

        var paymentMethod = new PaymentMethod
        {
            PaymentMethodId = paymentMethodId,
            CustomerId = customerId,
            Type = PaymentMethodType.CreditCard,
            Status = PaymentMethodStatus.Inactive
        };

        _mockPaymentMethodRepo.Setup(r => r.GetByIdAsync(paymentMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paymentMethod);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.InitiateTransferAsync(customerId, paymentMethodId, amount, purpose));

        _mockGateway.Verify(g => g.AuthorizeCardPaymentAsync(
            It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTransactionRepo.Verify(r => r.CreateTransferAsync(It.IsAny<FundTransfer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitiateTransferAsync_AuthorizationFailed_ThrowsInvalidOperationException()
    {
        // Arrange
        var customerId = "CUST-123";
        var paymentMethodId = "PM-001";
        var amount = 100.00m;
        var purpose = "Premium payment";

        var paymentMethod = new PaymentMethod
        {
            PaymentMethodId = paymentMethodId,
            CustomerId = customerId,
            Type = PaymentMethodType.CreditCard,
            Status = PaymentMethodStatus.Active,
            Card = new CardDetails
            {
                Token = "tok_123",
                Brand = "Visa",
                Last4 = "1234",
                ExpirationMonth = 12,
                ExpirationYear = 2025
            }
        };

        _mockPaymentMethodRepo.Setup(r => r.GetByIdAsync(paymentMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paymentMethod);

        _mockTransactionRepo.Setup(r => r.CreateTransferAsync(It.IsAny<FundTransfer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FundTransfer ft, CancellationToken ct) => ft);
        
        _mockTransactionRepo.Setup(r => r.UpdateTransferAsync(It.IsAny<FundTransfer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FundTransfer ft, CancellationToken ct) => ft);

        _mockGateway.Setup(g => g.AuthorizeCardPaymentAsync(
            It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Insufficient funds"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.InitiateTransferAsync(customerId, paymentMethodId, amount, purpose));

        _mockMessageSession.Verify(s => s.Publish(It.IsAny<FundsSettled>(), It.IsAny<PublishOptions>()), Times.Never);
    }

    #endregion

    #region ProcessRefundAsync Tests

    [Fact]
    public async Task ProcessRefundAsync_ValidRefund_ProcessesSuccessfully()
    {
        // Arrange
        var originalTransactionId = "TXN-001";
        var refundAmount = 50.00m;
        var reason = "Customer request";

        var originalTransfer = new FundTransfer
        {
            TransactionId = originalTransactionId,
            CustomerId = "CUST-123",
            PaymentMethodId = "PM-001",
            Amount = 100.00m,
            Direction = TransferDirection.Inbound,
            Status = TransferStatus.Settled,
            GatewayTransactionId = "gateway-123",
            SettledUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };

        _mockTransactionRepo.Setup(r => r.GetTransferByIdAsync(originalTransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTransfer);

        var refundResult = new RefundResult
        {
            IsSuccess = true,
            GatewayRefundId = "refund-123",
            ProcessedUtc = DateTimeOffset.UtcNow
        };

        _mockGateway.Setup(g => g.ProcessRefundAsync(
            It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundResult);

        _mockTransactionRepo.Setup(r => r.CreateRefundAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Refund rf, CancellationToken ct) => rf);

        _mockTransactionRepo.Setup(r => r.UpdateRefundAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Refund rf, CancellationToken ct) => rf);

        _mockMessageSession.Setup(s => s.Publish(It.IsAny<FundsRefunded>(), It.IsAny<PublishOptions>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.ProcessRefundAsync(
            originalTransactionId, refundAmount, reason);

        // Assert
        result.Should().NotBeNull();
        result.OriginalTransactionId.Should().Be(originalTransactionId);
        result.Amount.Should().Be(refundAmount);
        result.Reason.Should().Be(reason);
        result.GatewayRefundId.Should().Be("refund-123");
        result.ProcessedUtc.Should().NotBeNull();

        _mockTransactionRepo.Verify(r => r.UpdateRefundAsync(It.Is<Refund>(rf =>
            rf.OriginalTransactionId == originalTransactionId &&
            rf.Amount == refundAmount &&
            rf.GatewayRefundId == "refund-123"
        ), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        _mockMessageSession.Verify(s => s.Publish(It.Is<FundsRefunded>(e =>
            e.CustomerId == originalTransfer.CustomerId &&
            e.OriginalTransactionId == originalTransactionId &&
            e.Amount == refundAmount &&
            e.Reason == reason
        ), It.IsAny<PublishOptions>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRefundAsync_InvalidOriginalTransaction_ThrowsInvalidOperationException()
    {
        // Arrange
        var originalTransactionId = "TXN-INVALID";
        var refundAmount = 50.00m;
        var reason = "Customer request";

        _mockTransactionRepo.Setup(r => r.GetTransferByIdAsync(originalTransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FundTransfer?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.ProcessRefundAsync(originalTransactionId, refundAmount, reason));

        _mockGateway.Verify(g => g.ProcessRefundAsync(
            It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockMessageSession.Verify(s => s.Publish(It.IsAny<FundsRefunded>(), It.IsAny<PublishOptions>()), Times.Never);
    }

    [Fact]
    public async Task ProcessRefundAsync_RefundAmountExceedsOriginal_ThrowsInvalidOperationException()
    {
        // Arrange
        var originalTransactionId = "TXN-001";
        var refundAmount = 150.00m; // More than original 100.00
        var reason = "Customer request";

        var originalTransfer = new FundTransfer
        {
            TransactionId = originalTransactionId,
            CustomerId = "CUST-123",
            PaymentMethodId = "PM-001",
            Amount = 100.00m,
            Direction = TransferDirection.Inbound,
            Status = TransferStatus.Settled,
            GatewayTransactionId = "gateway-123",
            SettledUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };

        _mockTransactionRepo.Setup(r => r.GetTransferByIdAsync(originalTransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTransfer);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.ProcessRefundAsync(originalTransactionId, refundAmount, reason));

        _mockGateway.Verify(g => g.ProcessRefundAsync(
            It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}

