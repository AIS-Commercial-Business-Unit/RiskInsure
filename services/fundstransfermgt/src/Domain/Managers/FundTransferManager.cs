using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.FundTransferMgt.Domain.Models;
using RiskInsure.FundTransferMgt.Domain.Repositories;
using RiskInsure.FundTransferMgt.Domain.Services;
using RiskInsure.PublicContracts.Events;

namespace RiskInsure.FundTransferMgt.Domain.Managers;

public class FundTransferManager : IFundTransferManager
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IPaymentMethodRepository _paymentMethodRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IMessageSession _messageSession;
    private readonly ILogger<FundTransferManager> _logger;

    public FundTransferManager(
        ITransactionRepository transactionRepository,
        IPaymentMethodRepository paymentMethodRepository,
        IPaymentGateway paymentGateway,
        IMessageSession messageSession,
        ILogger<FundTransferManager> logger)
    {
        _transactionRepository = transactionRepository;
        _paymentMethodRepository = paymentMethodRepository;
        _paymentGateway = paymentGateway;
        _messageSession = messageSession;
        _logger = logger;
    }

    public async Task<FundTransfer> InitiateTransferAsync(
        string customerId,
        string paymentMethodId,
        decimal amount,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Initiating fund transfer for customer {CustomerId}, amount {Amount}, purpose {Purpose}",
            customerId, amount, purpose);

        // Validate payment method exists and belongs to customer
        var paymentMethod = await _paymentMethodRepository.GetByIdAsync(paymentMethodId, cancellationToken);
        if (paymentMethod == null)
        {
            throw new InvalidOperationException($"Payment method {paymentMethodId} not found");
        }

        if (paymentMethod.CustomerId != customerId)
        {
            throw new InvalidOperationException($"Payment method {paymentMethodId} does not belong to customer {customerId}");
        }

        if (paymentMethod.Status != PaymentMethodStatus.Active && paymentMethod.Status != PaymentMethodStatus.Validated)
        {
            throw new InvalidOperationException($"Payment method {paymentMethodId} is not active (status: {paymentMethod.Status})");
        }

        // Create transfer record
        var transfer = new FundTransfer
        {
            TransactionId = Guid.NewGuid().ToString(),
            CustomerId = customerId,
            PaymentMethodId = paymentMethodId,
            Amount = amount,
            Direction = TransferDirection.Inbound,
            Status = TransferStatus.Pending,
            Purpose = purpose
        };

        await _transactionRepository.CreateTransferAsync(transfer, cancellationToken);

        // Authorize payment through gateway
        transfer.Status = TransferStatus.Authorizing;
        await _transactionRepository.UpdateTransferAsync(transfer, cancellationToken);

        AuthorizationResult authResult;
        try
        {
            authResult = paymentMethod.Type switch
            {
                PaymentMethodType.CreditCard or PaymentMethodType.DebitCard =>
                    await _paymentGateway.AuthorizeCardPaymentAsync(
                        paymentMethod.Card!.Token, amount, customerId, transfer.TransactionId, cancellationToken),
                
                PaymentMethodType.Ach =>
                    await _paymentGateway.AuthorizeAchPaymentAsync(
                        paymentMethod.Ach!.EncryptedAccountNumber, amount, customerId, transfer.TransactionId, cancellationToken),
                
                PaymentMethodType.Wallet =>
                    await _paymentGateway.AuthorizeWalletPaymentAsync(
                        paymentMethod.Wallet!.Token, amount, customerId, transfer.TransactionId, cancellationToken),
                
                _ => throw new InvalidOperationException($"Unsupported payment method type: {paymentMethod.Type}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment gateway error for transaction {TransactionId}", transfer.TransactionId);
            
            transfer.Status = TransferStatus.Failed;
            transfer.FailedUtc = DateTimeOffset.UtcNow;
            transfer.FailureReason = "Gateway error: " + ex.Message;
            transfer.ErrorCode = "GATEWAY_ERROR";
            await _transactionRepository.UpdateTransferAsync(transfer, cancellationToken);
            
            throw;
        }

        if (!authResult.IsSuccess)
        {
            _logger.LogWarning(
                "Payment authorization failed for transaction {TransactionId}: {Reason}",
                transfer.TransactionId, authResult.FailureReason);
            
            transfer.Status = TransferStatus.Failed;
            transfer.FailedUtc = authResult.ProcessedUtc;
            transfer.FailureReason = authResult.FailureReason;
            transfer.ErrorCode = authResult.ErrorCode;
            await _transactionRepository.UpdateTransferAsync(transfer, cancellationToken);
            
            throw new InvalidOperationException($"Payment authorization failed: {authResult.FailureReason}");
        }

        // Authorization successful - settle
        transfer.Status = TransferStatus.Settling;
        transfer.AuthorizedUtc = authResult.ProcessedUtc;
        transfer.GatewayTransactionId = authResult.GatewayTransactionId;
        await _transactionRepository.UpdateTransferAsync(transfer, cancellationToken);

        // For mock gateway, settlement is immediate
        // Real gateway would have async settlement notification
        transfer.Status = TransferStatus.Settled;
        transfer.SettledUtc = DateTimeOffset.UtcNow;
        await _transactionRepository.UpdateTransferAsync(transfer, cancellationToken);

        _logger.LogInformation(
            "Transfer {TransactionId} settled successfully, amount {Amount}",
            transfer.TransactionId, transfer.Amount);

        // Publish FundsSettled event
        var fundsSettled = new FundsSettled(
            MessageId: Guid.NewGuid(),
            OccurredUtc: transfer.SettledUtc.Value,
            CustomerId: transfer.CustomerId,
            TransactionId: transfer.TransactionId,
            Amount: transfer.Amount,
            PaymentMethodId: transfer.PaymentMethodId,
            SettledUtc: transfer.SettledUtc.Value,
            IdempotencyKey: $"transfer-settled-{transfer.TransactionId}"
        );

        await _messageSession.Publish(fundsSettled);

        _logger.LogInformation(
            "Published FundsSettled event for transaction {TransactionId}",
            transfer.TransactionId);

        return transfer;
    }

    public async Task<Refund> ProcessRefundAsync(
        string originalTransactionId,
        decimal amount,
        string reason,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing refund for transaction {OriginalTransactionId}, amount {Amount}",
            originalTransactionId, amount);

        // Get original transaction
        var originalTransfer = await _transactionRepository.GetTransferByIdAsync(originalTransactionId, cancellationToken);
        if (originalTransfer == null)
        {
            throw new InvalidOperationException($"Original transaction {originalTransactionId} not found");
        }

        if (originalTransfer.Status != TransferStatus.Settled)
        {
            throw new InvalidOperationException($"Cannot refund transaction {originalTransactionId} - status is {originalTransfer.Status}, must be Settled");
        }

        if (amount > originalTransfer.Amount)
        {
            throw new InvalidOperationException($"Refund amount {amount} exceeds original transaction amount {originalTransfer.Amount}");
        }

        // Create refund record
        var refund = new Refund
        {
            RefundId = Guid.NewGuid().ToString(),
            CustomerId = originalTransfer.CustomerId,
            OriginalTransactionId = originalTransactionId,
            PaymentMethodId = originalTransfer.PaymentMethodId,
            Amount = amount,
            Status = TransferStatus.Pending,
            Reason = reason
        };

        await _transactionRepository.CreateRefundAsync(refund, cancellationToken);

        // Process refund through gateway
        RefundResult refundResult;
        try
        {
            refundResult = await _paymentGateway.ProcessRefundAsync(
                originalTransfer.GatewayTransactionId!,
                amount,
                originalTransfer.CustomerId,
                refund.RefundId,
                reason,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment gateway error for refund {RefundId}", refund.RefundId);
            
            refund.Status = TransferStatus.Failed;
            refund.FailedUtc = DateTimeOffset.UtcNow;
            refund.FailureReason = "Gateway error: " + ex.Message;
            await _transactionRepository.UpdateRefundAsync(refund, cancellationToken);
            
            throw;
        }

        if (!refundResult.IsSuccess)
        {
            _logger.LogWarning(
                "Refund failed for {RefundId}: {Reason}",
                refund.RefundId, refundResult.FailureReason);
            
            refund.Status = TransferStatus.Failed;
            refund.FailedUtc = refundResult.ProcessedUtc;
            refund.FailureReason = refundResult.FailureReason;
            await _transactionRepository.UpdateRefundAsync(refund, cancellationToken);
            
            throw new InvalidOperationException($"Refund failed: {refundResult.FailureReason}");
        }

        // Refund successful
        refund.Status = TransferStatus.Settled;
        refund.ProcessedUtc = refundResult.ProcessedUtc;
        refund.GatewayRefundId = refundResult.GatewayRefundId;
        await _transactionRepository.UpdateRefundAsync(refund, cancellationToken);

        _logger.LogInformation(
            "Refund {RefundId} processed successfully, amount {Amount}",
            refund.RefundId, refund.Amount);

        // Publish FundsRefunded event
        var fundsRefunded = new FundsRefunded(
            MessageId: Guid.NewGuid(),
            OccurredUtc: refund.ProcessedUtc.Value,
            CustomerId: refund.CustomerId,
            RefundId: refund.RefundId,
            OriginalTransactionId: refund.OriginalTransactionId,
            Amount: refund.Amount,
            RefundedUtc: refund.ProcessedUtc.Value,
            Reason: refund.Reason,
            IdempotencyKey: $"refund-processed-{refund.RefundId}"
        );

        await _messageSession.Publish(fundsRefunded);

        _logger.LogInformation(
            "Published FundsRefunded event for refund {RefundId}",
            refund.RefundId);

        return refund;
    }

    public async Task<FundTransfer?> GetTransferAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        return await _transactionRepository.GetTransferByIdAsync(transactionId, cancellationToken);
    }

    public async Task<List<FundTransfer>> GetCustomerTransfersAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        return await _transactionRepository.GetTransfersByCustomerIdAsync(customerId, cancellationToken);
    }
}
