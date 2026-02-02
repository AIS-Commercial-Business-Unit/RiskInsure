using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.Billing.Domain.Models;

namespace RiskInsure.Billing.Domain.Services.BillingDb;

/// <summary>
/// Cosmos DB implementation of IBillingAccountRepository.
/// Handles persistence with optimistic concurrency using ETag.
/// </summary>
public class BillingAccountRepository : IBillingAccountRepository
{
    private readonly Container _container;
    private readonly ILogger<BillingAccountRepository> _logger;

    public BillingAccountRepository(
        Container container,
        ILogger<BillingAccountRepository> logger)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BillingAccount?> GetByAccountIdAsync(
        string accountId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<BillingAccountDocument>(
                accountId,
                new PartitionKey(accountId),
                cancellationToken: cancellationToken);

            return MapToDomain(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInformation(
                "BillingAccount {AccountId} not found",
                accountId);
            return null;
        }
    }

    public async Task<BillingAccount?> GetByCustomerIdAsync(
        string customerId, 
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.type = 'BillingAccount' AND c.customerId = @customerId")
            .WithParameter("@customerId", customerId);

        var iterator = _container.GetItemQueryIterator<BillingAccountDocument>(query);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var account = response.FirstOrDefault();
            
            if (account != null)
            {
                _logger.LogInformation(
                    "Retrieved billing account for customer {CustomerId}",
                    customerId);
                return MapToDomain(account);
            }
        }

        _logger.LogInformation(
            "No billing account found for customer {CustomerId}",
            customerId);
        return null;
    }

    public async Task CreateAsync(
        BillingAccount account, 
        CancellationToken cancellationToken = default)
    {
        var document = MapToDocument(account);
        
        await _container.CreateItemAsync(
            document,
            new PartitionKey(account.AccountId),
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Created BillingAccount {AccountId} for customer {CustomerId}",
            account.AccountId,
            account.CustomerId);
    }

    public async Task UpdateAsync(
        BillingAccount account, 
        CancellationToken cancellationToken = default)
    {
        var document = MapToDocument(account);
        
        var requestOptions = new ItemRequestOptions
        {
            IfMatchEtag = account.ETag
        };

        await _container.ReplaceItemAsync(
            document,
            document.Id,
            new PartitionKey(account.AccountId),
            requestOptions,
            cancellationToken);

        _logger.LogInformation(
            "Updated BillingAccount {AccountId}",
            account.AccountId);
    }

    public async Task<BillingAccount> RecordPaymentAsync(
        string accountId,
        decimal amount,
        string referenceNumber,
        CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;
        var attempt = 0;

        while (attempt < maxRetries)
        {
            try
            {
                // Get current account state
                var account = await GetByAccountIdAsync(accountId, cancellationToken);
                if (account == null)
                {
                    throw new InvalidOperationException(
                        $"BillingAccount {accountId} not found");
                }

                // Apply payment
                account.TotalPaid += amount;
                account.LastUpdatedUtc = DateTimeOffset.UtcNow;

                // Save with optimistic concurrency
                await UpdateAsync(account, cancellationToken);

                _logger.LogInformation(
                    "Recorded payment {Amount} for account {AccountId} (ref: {ReferenceNumber}). TotalPaid: {TotalPaid}, Outstanding: {Outstanding}",
                    amount,
                    accountId,
                    referenceNumber,
                    account.TotalPaid,
                    account.OutstandingBalance);

                return account;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
            {
                attempt++;
                if (attempt >= maxRetries)
                {
                    _logger.LogError(
                        "Failed to record payment after {Retries} retries due to concurrency conflicts for account {AccountId}",
                        maxRetries,
                        accountId);
                    throw new InvalidOperationException(
                        $"Failed to update account {accountId} after {maxRetries} retries due to concurrent modifications",
                        ex);
                }

                _logger.LogWarning(
                    "Concurrency conflict on attempt {Attempt} for account {AccountId}, retrying...",
                    attempt,
                    accountId);

                // Exponential backoff
                await Task.Delay(100 * attempt, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Failed to record payment for account {accountId}");
    }

    private static BillingAccount MapToDomain(BillingAccountDocument document)
    {
        return new BillingAccount
        {
            AccountId = document.AccountId,
            CustomerId = document.CustomerId,
            PolicyNumber = document.PolicyNumber,
            TotalPremiumDue = document.TotalPremiumDue,
            TotalPaid = document.TotalPaid,
            Status = Enum.Parse<BillingAccountStatus>(document.Status),
            CreatedUtc = document.CreatedUtc,
            LastUpdatedUtc = document.LastUpdatedUtc,
            ETag = document.ETag
        };
    }

    private static BillingAccountDocument MapToDocument(BillingAccount account)
    {
        return new BillingAccountDocument
        {
            Id = account.AccountId,
            AccountId = account.AccountId,
            Type = "BillingAccount",
            CustomerId = account.CustomerId,
            PolicyNumber = account.PolicyNumber,
            TotalPremiumDue = account.TotalPremiumDue,
            TotalPaid = account.TotalPaid,
            Status = account.Status.ToString(),
            CreatedUtc = account.CreatedUtc,
            LastUpdatedUtc = account.LastUpdatedUtc,
            ETag = account.ETag
        };
    }
}
