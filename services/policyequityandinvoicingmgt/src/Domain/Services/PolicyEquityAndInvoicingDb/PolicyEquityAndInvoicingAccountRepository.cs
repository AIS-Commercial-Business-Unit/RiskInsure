using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Models;

namespace RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Services.PolicyEquityAndInvoicingDb;

/// <summary>
/// Cosmos DB implementation of IPolicyEquityAndInvoicingAccountRepository.
/// Handles persistence with optimistic concurrency using ETag.
/// </summary>
public class PolicyEquityAndInvoicingAccountRepository : IPolicyEquityAndInvoicingAccountRepository
{
    private readonly Container _container;
    private readonly ILogger<PolicyEquityAndInvoicingAccountRepository> _logger;

    public PolicyEquityAndInvoicingAccountRepository(
        Container container,
        ILogger<PolicyEquityAndInvoicingAccountRepository> logger)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PolicyEquityAndInvoicingAccount?> GetByAccountIdAsync(
        string accountId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<PolicyEquityAndInvoicingAccountDocument>(
                accountId,
                new PartitionKey(accountId),
                cancellationToken: cancellationToken);

            return MapToDomain(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInformation(
                "PolicyEquityAndInvoicingAccount {AccountId} not found",
                accountId);
            return null;
        }
    }

    public async Task<PolicyEquityAndInvoicingAccount?> GetByCustomerIdAsync(
        string customerId, 
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.type = 'PolicyEquityAndInvoicingAccount' AND c.customerId = @customerId")
            .WithParameter("@customerId", customerId);

        var iterator = _container.GetItemQueryIterator<PolicyEquityAndInvoicingAccountDocument>(query);
        
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

    public async Task<IEnumerable<PolicyEquityAndInvoicingAccount>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.type = 'PolicyEquityAndInvoicingAccount'");

        var iterator = _container.GetItemQueryIterator<PolicyEquityAndInvoicingAccountDocument>(query);
        var accounts = new List<PolicyEquityAndInvoicingAccount>();
        
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
        PolicyEquityAndInvoicingAccount account, 
        CancellationToken cancellationToken = default)
    {
        var document = MapToDocument(account);
        
        await _container.CreateItemAsync(
            document,
            new PartitionKey(account.AccountId),
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Created PolicyEquityAndInvoicingAccount {AccountId} for customer {CustomerId}",
            account.AccountId,
            account.CustomerId);
    }

    public async Task UpdateAsync(
        PolicyEquityAndInvoicingAccount account, 
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
            "Updated PolicyEquityAndInvoicingAccount {AccountId}",
            account.AccountId);
    }



    private static PolicyEquityAndInvoicingAccount MapToDomain(PolicyEquityAndInvoicingAccountDocument document)
    {
        return new PolicyEquityAndInvoicingAccount
        {
            AccountId = document.AccountId,
            CustomerId = document.CustomerId,
            PolicyNumber = document.PolicyNumber,
            PolicyHolderName = document.PolicyHolderName ?? "Unknown",
            CurrentPremiumOwed = document.CurrentPremiumOwed,
            TotalPremiumDue = document.TotalPremiumDue,
            TotalPaid = document.TotalPaid,
            Status = Enum.Parse<PolicyEquityAndInvoicingAccountStatus>(document.Status),
            PolicyEquityAndInvoicingCycle = Enum.TryParse<PolicyEquityAndInvoicingCycle>(document.PolicyEquityAndInvoicingCycle, out var cycle) 
                ? cycle 
                : PolicyEquityAndInvoicingCycle.Monthly,
            EffectiveDate = document.EffectiveDate,
            CreatedUtc = document.CreatedUtc,
            LastUpdatedUtc = document.LastUpdatedUtc,
            ETag = document.ETag
        };
    }

    private static PolicyEquityAndInvoicingAccountDocument MapToDocument(PolicyEquityAndInvoicingAccount account)
    {
        return new PolicyEquityAndInvoicingAccountDocument
        {
            Id = account.AccountId,
            AccountId = account.AccountId,
            Type = "PolicyEquityAndInvoicingAccount",
            CustomerId = account.CustomerId,
            PolicyNumber = account.PolicyNumber,
            PolicyHolderName = account.PolicyHolderName,
            CurrentPremiumOwed = account.CurrentPremiumOwed,
            TotalPremiumDue = account.TotalPremiumDue,
            TotalPaid = account.TotalPaid,
            Status = account.Status.ToString(),
            PolicyEquityAndInvoicingCycle = account.PolicyEquityAndInvoicingCycle.ToString(),
            EffectiveDate = account.EffectiveDate,
            CreatedUtc = account.CreatedUtc,
            LastUpdatedUtc = account.LastUpdatedUtc,
            ETag = account.ETag
        };
    }
}
