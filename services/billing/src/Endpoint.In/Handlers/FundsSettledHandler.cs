using NServiceBus;
using RiskInsure.PublicContracts.Events;
using RiskInsure.Billing.Domain.Managers;
using Microsoft.Extensions.Logging;

namespace RiskInsure.Billing.Endpoint.In.Handlers;

public class FundsSettledHandler : IHandleMessages<FundsSettled>
{
    private readonly IBillingPaymentManager _paymentManager;
    private readonly ILogger<FundsSettledHandler> _logger;

    public FundsSettledHandler(
        IBillingPaymentManager paymentManager,
        ILogger<FundsSettledHandler> logger)
    {
        _paymentManager = paymentManager;
        _logger = logger;
    }

    public async Task Handle(FundsSettled message, IMessageHandlerContext context)
    {
        _logger.LogInformation(
            "Processing FundsSettled for Customer {CustomerId}, Transaction {TransactionId}, Amount {Amount}",
            message.CustomerId, message.TransactionId, message.Amount);

        try
        {
            // Apply settled funds as payment to billing account
            await _paymentManager.ApplySettledFundsAsync(
                customerId: message.CustomerId,
                transactionId: message.TransactionId,
                amount: message.Amount,
                settledUtc: message.SettledUtc,
                idempotencyKey: message.IdempotencyKey,
                cancellationToken: context.CancellationToken);

            _logger.LogInformation(
                "Successfully applied settled funds for Customer {CustomerId}, Transaction {TransactionId}",
                message.CustomerId, message.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to apply settled funds for Customer {CustomerId}, Transaction {TransactionId}: {ErrorMessage}",
                message.CustomerId, message.TransactionId, ex.Message);
            throw; // Let NServiceBus retry
        }
    }
}
