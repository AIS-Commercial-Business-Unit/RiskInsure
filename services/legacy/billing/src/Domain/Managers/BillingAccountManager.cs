namespace RiskInsure.Billing.Domain.Managers;

using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.Billing.Domain.Contracts.Events;
using RiskInsure.Billing.Domain.Managers.DTOs;
using RiskInsure.Billing.Domain.Models;
using RiskInsure.Billing.Domain.Services.BillingDb;

/// <summary>
/// Manager for billing account lifecycle operations
/// Manages account creation, status changes, and premium updates
/// </summary>
public class BillingAccountManager : IBillingAccountManager
{
    private readonly IBillingAccountRepository _repository;
    private readonly IMessageSession _messageSession;
    private readonly ILogger<BillingAccountManager> _logger;

    public BillingAccountManager(
        IBillingAccountRepository repository,
        IMessageSession messageSession,
        ILogger<BillingAccountManager> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _messageSession = messageSession ?? throw new ArgumentNullException(nameof(messageSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new billing account for an insurance policy
    /// </summary>
    public async Task<BillingAccountResult> CreateBillingAccountAsync(
        CreateBillingAccountDto dto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating billing account {AccountId} for policy {PolicyNumber}",
            dto.AccountId, dto.PolicyNumber);

        try
        {
            // Business Rule 1: Account ID must not already exist
            var existingAccount = await _repository.GetByAccountIdAsync(dto.AccountId, cancellationToken);
            if (existingAccount != null)
            {
                _logger.LogWarning(
                    "Account {AccountId} already exists - idempotent duplicate detected",
                    dto.AccountId);
                return BillingAccountResult.Success(dto.AccountId);
            }

            // Business Rule 2: Policy number must be unique
            var existingPolicy = await _repository.GetByCustomerIdAsync(dto.CustomerId, cancellationToken);
            if (existingPolicy != null && existingPolicy.PolicyNumber == dto.PolicyNumber)
            {
                return BillingAccountResult.Failure(
                    $"Policy number {dto.PolicyNumber} already exists for customer {dto.CustomerId}",
                    "DUPLICATE_POLICY_NUMBER");
            }

            // Business Rule 3: Premium must be >= 0
            if (dto.CurrentPremiumOwed < 0)
            {
                return BillingAccountResult.Failure(
                    "Premium owed cannot be negative",
                    "NEGATIVE_PREMIUM");
            }

            // Business Rule 4: Effective date cannot be too far in the past (> 90 days)
            var daysSinceEffective = (DateTimeOffset.UtcNow - dto.EffectiveDate).TotalDays;
            if (daysSinceEffective > 90)
            {
                return BillingAccountResult.Failure(
                    "Effective date cannot be more than 90 days in the past",
                    "INVALID_EFFECTIVE_DATE");
            }

            // Create the account
            var account = new BillingAccount
            {
                AccountId = dto.AccountId,
                CustomerId = dto.CustomerId,
                PolicyNumber = dto.PolicyNumber,
                PolicyHolderName = dto.PolicyHolderName,
                CurrentPremiumOwed = dto.CurrentPremiumOwed,
                TotalPremiumDue = dto.CurrentPremiumOwed, // Initially same as current premium
                TotalPaid = 0,
                Status = BillingAccountStatus.Pending, // Created in Pending state
                BillingCycle = dto.BillingCycle,
                EffectiveDate = dto.EffectiveDate,
                CreatedUtc = DateTimeOffset.UtcNow,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await _repository.CreateAsync(account, cancellationToken);
            sw.Stop();
            _logger.LogInformation("⏱️ DB CreateAsync took {ElapsedMs}ms", sw.ElapsedMilliseconds);

            // Publish BillingAccountCreated event
            var @event = new BillingAccountCreated(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                AccountId: account.AccountId,
                CustomerId: account.CustomerId,
                PolicyNumber: account.PolicyNumber,
                PolicyHolderName: account.PolicyHolderName,
                CurrentPremiumOwed: account.CurrentPremiumOwed,
                BillingCycle: account.BillingCycle,
                EffectiveDate: account.EffectiveDate,
                IdempotencyKey: $"account-created-{account.AccountId}"
            );

            sw.Restart();
            await _messageSession.Publish(@event);
            sw.Stop();
            _logger.LogInformation("⏱️ Event Publish took {ElapsedMs}ms", sw.ElapsedMilliseconds);

            _logger.LogInformation(
                "Successfully created billing account {AccountId} for policy {PolicyNumber}",
                account.AccountId, account.PolicyNumber);

            return BillingAccountResult.Success(account.AccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error creating billing account {AccountId}: {ErrorMessage}",
                dto.AccountId, ex.Message);

            return BillingAccountResult.Failure(
                $"Failed to create account: {ex.Message}",
                "ACCOUNT_CREATION_ERROR",
                isRetryable: true);
        }
    }

    /// <summary>
    /// Updates the premium owed on an existing account
    /// </summary>
    public async Task<BillingAccountResult> UpdatePremiumOwedAsync(
        UpdatePremiumOwedDto dto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating premium for account {AccountId} to {NewPremiumOwed}",
            dto.AccountId, dto.NewPremiumOwed);

        try
        {
            // Get account
            var account = await _repository.GetByAccountIdAsync(dto.AccountId, cancellationToken);
            if (account == null)
            {
                return BillingAccountResult.Failure(
                    $"Account {dto.AccountId} not found",
                    "ACCOUNT_NOT_FOUND");
            }

            // Business Rule 1: Account must not be closed
            if (account.Status == BillingAccountStatus.Closed)
            {
                return BillingAccountResult.Failure(
                    "Cannot update premium on a closed account",
                    "ACCOUNT_CLOSED");
            }

            // Business Rule 2: New premium must be >= 0
            if (dto.NewPremiumOwed < 0)
            {
                return BillingAccountResult.Failure(
                    "Premium owed cannot be negative",
                    "NEGATIVE_PREMIUM");
            }

            var oldPremium = account.CurrentPremiumOwed;

            // Update premium
            account.CurrentPremiumOwed = dto.NewPremiumOwed;
            account.TotalPremiumDue = dto.NewPremiumOwed;
            account.LastUpdatedUtc = DateTimeOffset.UtcNow;

            await _repository.UpdateAsync(account, cancellationToken);

            // Publish PremiumOwedUpdated event
            var @event = new PremiumOwedUpdated(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                AccountId: account.AccountId,
                OldPremiumOwed: oldPremium,
                NewPremiumOwed: dto.NewPremiumOwed,
                ChangeReason: dto.ChangeReason,
                IdempotencyKey: $"premium-updated-{account.AccountId}-{DateTimeOffset.UtcNow.Ticks}"
            );

            await _messageSession.Publish(@event);

            _logger.LogInformation(
                "Successfully updated premium for account {AccountId} from {OldPremium} to {NewPremium}",
                account.AccountId, oldPremium, dto.NewPremiumOwed);

            return BillingAccountResult.Success(account.AccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error updating premium for account {AccountId}: {ErrorMessage}",
                dto.AccountId, ex.Message);

            return BillingAccountResult.Failure(
                $"Failed to update premium: {ex.Message}",
                "PREMIUM_UPDATE_ERROR",
                isRetryable: true);
        }
    }

    /// <summary>
    /// Activates a pending billing account
    /// </summary>
    public async Task<BillingAccountResult> ActivateAccountAsync(
        string accountId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Activating account {AccountId}", accountId);

        try
        {
            // Get account
            var account = await _repository.GetByAccountIdAsync(accountId, cancellationToken);
            if (account == null)
            {
                return BillingAccountResult.Failure(
                    $"Account {accountId} not found",
                    "ACCOUNT_NOT_FOUND");
            }

            // Business Rule 1: Account must be in Pending status
            if (account.Status != BillingAccountStatus.Pending)
            {
                _logger.LogWarning(
                    "Account {AccountId} is already in {Status} status",
                    accountId, account.Status);
                return BillingAccountResult.Success(accountId); // Idempotent
            }

            // Activate account
            account.Status = BillingAccountStatus.Active;
            account.LastUpdatedUtc = DateTimeOffset.UtcNow;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await _repository.UpdateAsync(account, cancellationToken);
            sw.Stop();
            _logger.LogInformation("⏱️ DB UpdateAsync took {ElapsedMs}ms", sw.ElapsedMilliseconds);

            // Publish AccountActivated event
            var @event = new AccountActivated(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                AccountId: account.AccountId,
                PolicyNumber: account.PolicyNumber,
                IdempotencyKey: $"account-activated-{account.AccountId}"
            );

            sw.Restart();
            await _messageSession.Publish(@event);
            sw.Stop();
            _logger.LogInformation("⏱️ Event Publish took {ElapsedMs}ms", sw.ElapsedMilliseconds);

            _logger.LogInformation(
                "Successfully activated account {AccountId}",
                account.AccountId);

            return BillingAccountResult.Success(account.AccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error activating account {AccountId}: {ErrorMessage}",
                accountId, ex.Message);

            return BillingAccountResult.Failure(
                $"Failed to activate account: {ex.Message}",
                "ACCOUNT_ACTIVATION_ERROR",
                isRetryable: true);
        }
    }

    /// <summary>
    /// Suspends an active billing account
    /// </summary>
    public async Task<BillingAccountResult> SuspendAccountAsync(
        string accountId,
        string suspensionReason,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Suspending account {AccountId} - Reason: {Reason}",
            accountId, suspensionReason);

        try
        {
            // Get account
            var account = await _repository.GetByAccountIdAsync(accountId, cancellationToken);
            if (account == null)
            {
                return BillingAccountResult.Failure(
                    $"Account {accountId} not found",
                    "ACCOUNT_NOT_FOUND");
            }

            // Business Rule 1: Cannot suspend a closed account
            if (account.Status == BillingAccountStatus.Closed)
            {
                return BillingAccountResult.Failure(
                    "Cannot suspend a closed account",
                    "ACCOUNT_CLOSED");
            }

            // Business Rule 2: If already suspended, just return success (idempotent)
            if (account.Status == BillingAccountStatus.Suspended)
            {
                _logger.LogWarning(
                    "Account {AccountId} is already suspended",
                    accountId);
                return BillingAccountResult.Success(accountId);
            }

            // Suspend account
            account.Status = BillingAccountStatus.Suspended;
            account.LastUpdatedUtc = DateTimeOffset.UtcNow;

            await _repository.UpdateAsync(account, cancellationToken);

            // Publish AccountSuspended event
            var @event = new AccountSuspended(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                AccountId: account.AccountId,
                PolicyNumber: account.PolicyNumber,
                SuspensionReason: suspensionReason,
                IdempotencyKey: $"account-suspended-{account.AccountId}-{DateTimeOffset.UtcNow.Ticks}"
            );

            await _messageSession.Publish(@event);

            _logger.LogInformation(
                "Successfully suspended account {AccountId}",
                account.AccountId);

            return BillingAccountResult.Success(account.AccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error suspending account {AccountId}: {ErrorMessage}",
                accountId, ex.Message);

            return BillingAccountResult.Failure(
                $"Failed to suspend account: {ex.Message}",
                "ACCOUNT_SUSPENSION_ERROR",
                isRetryable: true);
        }
    }

    /// <summary>
    /// Closes a billing account
    /// </summary>
    public async Task<BillingAccountResult> CloseAccountAsync(
        string accountId,
        string closureReason,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Closing account {AccountId} - Reason: {Reason}",
            accountId, closureReason);

        try
        {
            // Get account
            var account = await _repository.GetByAccountIdAsync(accountId, cancellationToken);
            if (account == null)
            {
                return BillingAccountResult.Failure(
                    $"Account {accountId} not found",
                    "ACCOUNT_NOT_FOUND");
            }

            // Business Rule 1: If already closed, just return success (idempotent)
            if (account.Status == BillingAccountStatus.Closed)
            {
                _logger.LogWarning(
                    "Account {AccountId} is already closed",
                    accountId);
                return BillingAccountResult.Success(accountId);
            }

            // Business Rule 2: Warn if closing with outstanding balance
            if (account.OutstandingBalance > 0)
            {
                _logger.LogWarning(
                    "Closing account {AccountId} with outstanding balance {Balance}",
                    accountId, account.OutstandingBalance);
            }

            var finalBalance = account.OutstandingBalance;

            // Close account
            account.Status = BillingAccountStatus.Closed;
            account.LastUpdatedUtc = DateTimeOffset.UtcNow;

            await _repository.UpdateAsync(account, cancellationToken);

            // Publish AccountClosed event
            var @event = new AccountClosed(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                AccountId: account.AccountId,
                PolicyNumber: account.PolicyNumber,
                ClosureReason: closureReason,
                FinalOutstandingBalance: finalBalance,
                IdempotencyKey: $"account-closed-{account.AccountId}"
            );

            await _messageSession.Publish(@event);

            _logger.LogInformation(
                "Successfully closed account {AccountId} with final balance {Balance}",
                account.AccountId, finalBalance);

            return BillingAccountResult.Success(account.AccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error closing account {AccountId}: {ErrorMessage}",
                accountId, ex.Message);

            return BillingAccountResult.Failure(
                $"Failed to close account: {ex.Message}",
                "ACCOUNT_CLOSURE_ERROR",
                isRetryable: true);
        }
    }

    /// <summary>
    /// Updates the billing cycle for an account
    /// </summary>
    public async Task<BillingAccountResult> UpdateBillingCycleAsync(
        UpdateBillingCycleDto dto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating billing cycle for account {AccountId} to {NewCycle}",
            dto.AccountId, dto.NewBillingCycle);

        try
        {
            // Get account
            var account = await _repository.GetByAccountIdAsync(dto.AccountId, cancellationToken);
            if (account == null)
            {
                return BillingAccountResult.Failure(
                    $"Account {dto.AccountId} not found",
                    "ACCOUNT_NOT_FOUND");
            }

            // Business Rule 1: Account must not be closed
            if (account.Status == BillingAccountStatus.Closed)
            {
                return BillingAccountResult.Failure(
                    "Cannot update billing cycle on a closed account",
                    "ACCOUNT_CLOSED");
            }

            var oldCycle = account.BillingCycle;

            // Update billing cycle
            account.BillingCycle = dto.NewBillingCycle;
            account.LastUpdatedUtc = DateTimeOffset.UtcNow;

            await _repository.UpdateAsync(account, cancellationToken);

            // Publish BillingCycleUpdated event
            var @event = new BillingCycleUpdated(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                AccountId: account.AccountId,
                OldBillingCycle: oldCycle,
                NewBillingCycle: dto.NewBillingCycle,
                ChangeReason: dto.ChangeReason,
                IdempotencyKey: $"billing-cycle-updated-{account.AccountId}-{DateTimeOffset.UtcNow.Ticks}"
            );

            await _messageSession.Publish(@event);

            _logger.LogInformation(
                "Successfully updated billing cycle for account {AccountId} from {OldCycle} to {NewCycle}",
                account.AccountId, oldCycle, dto.NewBillingCycle);

            return BillingAccountResult.Success(account.AccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error updating billing cycle for account {AccountId}: {ErrorMessage}",
                dto.AccountId, ex.Message);

            return BillingAccountResult.Failure(
                $"Failed to update billing cycle: {ex.Message}",
                "BILLING_CYCLE_UPDATE_ERROR",
                isRetryable: true);
        }
    }

    /// <summary>
    /// Retrieves all billing accounts
    /// </summary>
    public async Task<IEnumerable<BillingAccount>> GetAllAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all billing accounts");

        var accounts = await _repository.GetAllAsync(cancellationToken);

        _logger.LogInformation("Retrieved {Count} billing accounts", accounts.Count());

        return accounts;
    }

    /// <summary>
    /// Retrieves a single billing account by ID
    /// </summary>
    public async Task<BillingAccount?> GetAccountByIdAsync(
        string accountId, 
        CancellationToken cancellationToken = default)
    {
        // Repository logs this operation
        var account = await _repository.GetByAccountIdAsync(accountId, cancellationToken);
        return account;
    }
}
