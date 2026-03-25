using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.Billing.Domain.Contracts.Commands;
using RiskInsure.Billing.Domain.Managers;
using RiskInsure.Billing.Domain.Managers.DTOs;

namespace RiskInsure.Billing.Endpoint.In.Handlers;

/// <summary>
/// Handles RecordPayment commands by delegating to the BillingPaymentManager.
/// Thin handler following constitutional Principle VII - protocol translation only.
/// </summary>
public class RecordPaymentHandler : IHandleMessages<RecordPayment>
{
    private readonly IBillingPaymentManager _manager;
    private readonly ILogger<RecordPaymentHandler> _logger;

    public RecordPaymentHandler(
        IBillingPaymentManager manager,
        ILogger<RecordPaymentHandler> logger)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(RecordPayment message, IMessageHandlerContext context)
    {
        _logger.LogInformation(
            "Processing RecordPayment command for account {AccountId}, amount {Amount}, reference {ReferenceNumber}",
            message.AccountId,
            message.Amount,
            message.ReferenceNumber);

        // Map command to DTO (protocol translation)
        var dto = new RecordPaymentDto
        {
            AccountId = message.AccountId,
            Amount = message.Amount,
            ReferenceNumber = message.ReferenceNumber,
            IdempotencyKey = message.IdempotencyKey,
            OccurredUtc = message.OccurredUtc
        };

        // Delegate to Manager (business logic)
        var result = await _manager.RecordPaymentAsync(dto, context.CancellationToken);

        // Handle result
        if (!result.IsSuccess)
        {
            _logger.LogError(
                "Failed to record payment for account {AccountId}: {ErrorMessage} ({ErrorCode})",
                message.AccountId, result.ErrorMessage, result.ErrorCode);

            // If retryable, throw to trigger NServiceBus retry policy
            if (result.IsRetryable)
            {
                throw new InvalidOperationException(
                    $"Failed to record payment: {result.ErrorMessage}");
            }

            // Non-retryable errors - move to error queue
            throw new InvalidOperationException(
                $"Business rule violation: {result.ErrorMessage}");
        }

        if (result.WasDuplicate)
        {
            _logger.LogInformation(
                "Duplicate payment detected for account {AccountId}, reference {ReferenceNumber} - idempotent handling",
                message.AccountId, message.ReferenceNumber);
        }
        else
        {
            _logger.LogInformation(
                "Successfully recorded payment for account {AccountId}. TotalPaid: {TotalPaid}, Outstanding: {Outstanding}",
                message.AccountId,
                result.UpdatedAccount!.TotalPaid,
                result.UpdatedAccount.OutstandingBalance);
        }
    }
}
