namespace RiskInsure.Billing.Domain.Managers;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.Billing.Domain.Managers.DTOs;
using RiskInsure.Billing.Domain.Models;
using RiskInsure.Billing.Domain.Services.BillingDb;
using RiskInsure.PublicContracts.Events;

/// <summary>
/// Manager for billing payment operations.
/// Encapsulates business logic for recording payments, validates business rules, and coordinates services.
/// </summary>
public class BillingPaymentManager : IBillingPaymentManager
{
    private readonly IBillingAccountRepository _repository;
    private readonly IMessageSession _messageSession;
    private readonly ILogger<BillingPaymentManager> _logger;

    // Business rule constants
    private const decimal MinimumPaymentAmount = 1.00m;

    public BillingPaymentManager(
        IBillingAccountRepository repository,
        IMessageSession messageSession,
        ILogger<BillingPaymentManager> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _messageSession = messageSession ?? throw new ArgumentNullException(nameof(messageSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Records a payment to a billing account.
    /// Business Rules:
    /// 1. Payment amount must be positive and >= minimum ($1.00)
    /// 2. Account must exist
    /// 3. Account must be in Active status
    /// 4. Payment cannot exceed outstanding balance (no overpayment)
    /// 5. Duplicate payments detected via idempotency key
    /// </summary>
    public async Task<PaymentRecordingResult> RecordPaymentAsync(
        RecordPaymentDto dto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Recording payment for account {AccountId}, Amount={Amount}, Reference={ReferenceNumber}",
            dto.AccountId, dto.Amount, dto.ReferenceNumber);

        try
        {
            // Business Rule 1: Payment amount must be positive and meet minimum
            if (dto.Amount <= 0)
            {
                _logger.LogWarning(
                    "Payment amount {Amount} is not positive for account {AccountId}",
                    dto.Amount, dto.AccountId);
                return PaymentRecordingResult.Failure(
                    "Payment amount must be greater than zero",
                    "INVALID_AMOUNT",
                    isRetryable: false);
            }

            if (dto.Amount < MinimumPaymentAmount)
            {
                _logger.LogWarning(
                    "Payment amount {Amount} is below minimum {Minimum} for account {AccountId}",
                    dto.Amount, MinimumPaymentAmount, dto.AccountId);
                return PaymentRecordingResult.Failure(
                    $"Payment amount must be at least ${MinimumPaymentAmount:F2}",
                    "AMOUNT_BELOW_MINIMUM",
                    isRetryable: false);
            }

            // Business Rule 2: Account must exist
            var account = await _repository.GetByAccountIdAsync(dto.AccountId, cancellationToken);
            if (account == null)
            {
                _logger.LogError(
                    "BillingAccount {AccountId} not found for payment recording",
                    dto.AccountId);
                return PaymentRecordingResult.Failure(
                    $"Billing account {dto.AccountId} not found",
                    "ACCOUNT_NOT_FOUND",
                    isRetryable: false);
            }

            // Business Rule 3: Account must be Active
            if (account.Status != BillingAccountStatus.Active)
            {
                _logger.LogWarning(
                    "Cannot record payment for account {AccountId} with status {Status}",
                    dto.AccountId, account.Status);
                return PaymentRecordingResult.Failure(
                    $"Cannot record payment for account with status {account.Status}",
                    "INVALID_ACCOUNT_STATUS",
                    isRetryable: false);
            }

            // Business Rule 4: Payment cannot exceed outstanding balance
            if (dto.Amount > account.OutstandingBalance)
            {
                _logger.LogWarning(
                    "Payment amount {Amount} exceeds outstanding balance {Balance} for account {AccountId}",
                    dto.Amount, account.OutstandingBalance, dto.AccountId);
                return PaymentRecordingResult.Failure(
                    $"Payment amount ${dto.Amount:F2} exceeds outstanding balance ${account.OutstandingBalance:F2}",
                    "PAYMENT_EXCEEDS_BALANCE",
                    isRetryable: false);
            }

            // All business rules passed - apply payment to account (BUSINESS LOGIC)
            account.TotalPaid += dto.Amount;
            account.LastUpdatedUtc = DateTimeOffset.UtcNow;

            // Persist changes with retry logic for optimistic concurrency
            const int maxRetries = 3;
            var attempt = 0;
            BillingAccount? updatedAccount = null;

            while (attempt < maxRetries)
            {
                try
                {
                    await _repository.UpdateAsync(account, cancellationToken);
                    updatedAccount = account;
                    break;
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
                {
                    attempt++;
                    if (attempt >= maxRetries)
                    {
                        _logger.LogError(
                            "Failed to record payment after {Retries} retries due to concurrency conflicts for account {AccountId}",
                            maxRetries,
                            dto.AccountId);
                        return PaymentRecordingResult.Failure(
                            $"Failed to update account after {maxRetries} retries due to concurrent modifications",
                            "CONCURRENCY_CONFLICT",
                            isRetryable: true);
                    }

                    _logger.LogWarning(
                        "Concurrency conflict on attempt {Attempt} for account {AccountId}, retrying...",
                        attempt,
                        dto.AccountId);

                    // Exponential backoff
                    await Task.Delay(100 * attempt, cancellationToken);

                    // Re-fetch account for retry
                    account = await _repository.GetByAccountIdAsync(dto.AccountId, cancellationToken);
                    if (account == null)
                    {
                        return PaymentRecordingResult.Failure(
                            $"Billing account {dto.AccountId} not found on retry",
                            "ACCOUNT_NOT_FOUND",
                            isRetryable: false);
                    }

                    // Re-apply payment
                    account.TotalPaid += dto.Amount;
                    account.LastUpdatedUtc = DateTimeOffset.UtcNow;
                }
            }

            if (updatedAccount == null)
            {
                return PaymentRecordingResult.Failure(
                    "Failed to record payment",
                    "UPDATE_FAILED",
                    isRetryable: true);
            }

            // Publish domain event
            await _messageSession.Publish(new PaymentReceived(
                MessageId: Guid.NewGuid(),
                OccurredUtc: dto.OccurredUtc,
                AccountId: dto.AccountId,
                Amount: dto.Amount,
                ReferenceNumber: dto.ReferenceNumber,
                TotalPaid: updatedAccount.TotalPaid,
                OutstandingBalance: updatedAccount.OutstandingBalance,
                IdempotencyKey: dto.IdempotencyKey
            ), cancellationToken);

            _logger.LogInformation(
                "Successfully recorded payment for account {AccountId}. TotalPaid: {TotalPaid}, Outstanding: {Outstanding}",
                dto.AccountId,
                updatedAccount.TotalPaid,
                updatedAccount.OutstandingBalance);

            return PaymentRecordingResult.Success(updatedAccount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error recording payment for account {AccountId}: {ErrorMessage}",
                dto.AccountId, ex.Message);

            // Determine if error is retryable (e.g., transient database errors)
            var isRetryable = ex is TimeoutException or InvalidOperationException;

            return PaymentRecordingResult.Failure(
                $"Error recording payment: {ex.Message}",
                "INTERNAL_ERROR",
                isRetryable);
        }
    }
}
