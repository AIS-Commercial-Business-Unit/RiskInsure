using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.Billing.Domain.Contracts.Commands;
using RiskInsure.Billing.Domain.Services.BillingDb;
using RiskInsure.PublicContracts.Events;

namespace RiskInsure.Billing.Endpoint.In.Handlers;

/// <summary>
/// Handles RecordPayment commands by delegating to the domain repository
/// and publishing PaymentReceived events.
/// Thin handler following constitutional Principle VII.
/// </summary>
public class RecordPaymentHandler : IHandleMessages<RecordPayment>
{
    private readonly IBillingAccountRepository _repository;
    private readonly ILogger<RecordPaymentHandler> _logger;

    public RecordPaymentHandler(
        IBillingAccountRepository repository,
        ILogger<RecordPaymentHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(RecordPayment message, IMessageHandlerContext context)
    {
        _logger.LogInformation(
            "Processing RecordPayment for account {AccountId}, amount {Amount}, reference {ReferenceNumber}",
            message.AccountId,
            message.Amount,
            message.ReferenceNumber);

        // Verify account exists
        var account = await _repository.GetByAccountIdAsync(message.AccountId, context.CancellationToken);
        if (account == null)
        {
            _logger.LogError(
                "BillingAccount {AccountId} not found for payment recording",
                message.AccountId);
            throw new InvalidOperationException(
                $"BillingAccount {message.AccountId} not found");
        }

        // Record payment with optimistic concurrency
        var updatedAccount = await _repository.RecordPaymentAsync(
            message.AccountId,
            message.Amount,
            message.ReferenceNumber,
            context.CancellationToken);

        // Publish event for downstream consumers
        await context.Publish(new PaymentReceived(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            AccountId: message.AccountId,
            Amount: message.Amount,
            ReferenceNumber: message.ReferenceNumber,
            TotalPaid: updatedAccount.TotalPaid,
            OutstandingBalance: updatedAccount.OutstandingBalance,
            IdempotencyKey: message.IdempotencyKey
        ));

        _logger.LogInformation(
            "Successfully recorded payment for account {AccountId}. TotalPaid: {TotalPaid}, Outstanding: {Outstanding}",
            message.AccountId,
            updatedAccount.TotalPaid,
            updatedAccount.OutstandingBalance);
    }
}
