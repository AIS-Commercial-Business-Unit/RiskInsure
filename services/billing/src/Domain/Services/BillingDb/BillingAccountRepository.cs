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

    public async Task<IEnumerable<BillingAccount>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.type = 'BillingAccount'");

        var iterator = _container.GetItemQueryIterator<BillingAccountDocument>(query);
        var accounts = new List<BillingAccount>();
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            accounts.AddRange(response.Select(MapToDomain));
        }

        _logger.LogInformation(
            "Retrieved {Count} billing accounts",
            accounts.Count);
        
        return accounts;
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



    private static BillingAccount MapToDomain(BillingAccountDocument document)
    {
        return new BillingAccount
        {
            AccountId = document.AccountId,
            CustomerId = document.CustomerId,
            PolicyNumber = document.PolicyNumber,
            PolicyHolderName = document.PolicyHolderName ?? "Unknown",
            CurrentPremiumOwed = document.CurrentPremiumOwed,
            TotalPremiumDue = document.TotalPremiumDue,
            TotalPaid = document.TotalPaid,
            Status = Enum.Parse<BillingAccountStatus>(document.Status),
            BillingCycle = Enum.TryParse<BillingCycle>(document.BillingCycle, out var cycle) 
                ? cycle 
                : BillingCycle.Monthly,
            EffectiveDate = document.EffectiveDate,
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
            PolicyHolderName = account.PolicyHolderName,
            CurrentPremiumOwed = account.CurrentPremiumOwed,
            TotalPremiumDue = account.TotalPremiumDue,
            TotalPaid = account.TotalPaid,
            Status = account.Status.ToString(),
            BillingCycle = account.BillingCycle.ToString(),
            EffectiveDate = account.EffectiveDate,
            CreatedUtc = account.CreatedUtc,
            LastUpdatedUtc = account.LastUpdatedUtc,
            ETag = account.ETag
        };
    }
}
