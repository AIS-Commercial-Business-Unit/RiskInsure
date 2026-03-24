using NServiceBus;
using RiskInsure.PublicContracts.Events;
using RiskInsure.Billing.Domain.Managers;
using Microsoft.Extensions.Logging;

namespace RiskInsure.Billing.Endpoint.In.Handlers;

public class FundsRefundedHandler : IHandleMessages<FundsRefunded>
{
    private readonly IBillingPaymentManager _paymentManager;
    private readonly ILogger<FundsRefundedHandler> _logger;

    public FundsRefundedHandler(
        IBillingPaymentManager paymentManager,
        ILogger<FundsRefundedHandler> logger)
    {
        _paymentManager = paymentManager;
        _logger = logger;
    }

    public async Task Handle(FundsRefunded message, IMessageHandlerContext context)
    {
        _logger.LogInformation(
            "Processing FundsRefunded for Customer {CustomerId}, Refund {RefundId}, Amount {Amount}",
            message.CustomerId, message.RefundId, message.Amount);

        try
        {
            // Reverse payment application for refunded funds
            await _paymentManager.ApplyRefundAsync(
                customerId: message.CustomerId,
                refundId: message.RefundId,
                originalTransactionId: message.OriginalTransactionId,
                amount: message.Amount,
                refundedUtc: message.RefundedUtc,
                reason: message.Reason,
                idempotencyKey: message.IdempotencyKey,
                cancellationToken: context.CancellationToken);

            _logger.LogInformation(
                "Successfully applied refund for Customer {CustomerId}, Refund {RefundId}",
                message.CustomerId, message.RefundId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to apply refund for Customer {CustomerId}, Refund {RefundId}: {ErrorMessage}",
                message.CustomerId, message.RefundId, ex.Message);
            throw; // Let NServiceBus retry
        }
    }
}
