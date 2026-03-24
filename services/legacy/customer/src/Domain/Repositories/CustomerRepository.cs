namespace RiskInsure.Customer.Domain.Repositories;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.Customer.Domain.Models;

public class CustomerRepository : ICustomerRepository
{
    private readonly Container _container;
    private readonly ILogger<CustomerRepository> _logger;

    public CustomerRepository(Container container, ILogger<CustomerRepository> logger)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Models.Customer?> GetByIdAsync(string customerId)
    {
        try
        {
            var response = await _container.ReadItemAsync<Models.Customer>(
                customerId,
                new PartitionKey(customerId));

            _logger.LogInformation(
                "Retrieved customer {CustomerId}",
                customerId);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "Customer {CustomerId} not found",
                customerId);
            return null;
        }
    }

    public async Task<Models.Customer?> GetByEmailAsync(string email)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.documentType = @documentType AND c.email = @email")
            .WithParameter("@documentType", "Customer")
            .WithParameter("@email", email);

        var iterator = _container.GetItemQueryIterator<Models.Customer>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            var customer = response.FirstOrDefault();
            
            if (customer != null)
            {
                _logger.LogInformation(
                    "Found customer by email {Email}: {CustomerId}",
                    email, customer.CustomerId);
                return customer;
            }
        }

        _logger.LogInformation(
            "No customer found with email {Email}",
            email);
        return null;
    }

    public async Task<Models.Customer> CreateAsync(Models.Customer customer)
    {
        customer.CreatedUtc = DateTimeOffset.UtcNow;
        customer.UpdatedUtc = DateTimeOffset.UtcNow;

        var response = await _container.CreateItemAsync(
            customer,
            new PartitionKey(customer.CustomerId));

        _logger.LogInformation(
            "Created customer {CustomerId} with email {Email}",
            customer.CustomerId, customer.Email);

        return response.Resource;
    }

    public async Task<Models.Customer> UpdateAsync(Models.Customer customer)
    {
        customer.UpdatedUtc = DateTimeOffset.UtcNow;

        var response = await _container.ReplaceItemAsync(
            customer,
            customer.Id,
            new PartitionKey(customer.CustomerId),
            new ItemRequestOptions { IfMatchEtag = customer.ETag });

        _logger.LogInformation(
            "Updated customer {CustomerId}",
            customer.CustomerId);

        return response.Resource;
    }

    public async Task DeleteAsync(string customerId)
    {
        await _container.DeleteItemAsync<Models.Customer>(
            customerId,
            new PartitionKey(customerId));

        _logger.LogInformation(
            "Deleted customer {CustomerId}",
            customerId);
    }

    public async Task<IEnumerable<Models.Customer>> GetByStatusAsync(string status)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.documentType = @documentType AND c.status = @status")
            .WithParameter("@documentType", "Customer")
            .WithParameter("@status", status);

        var results = new List<Models.Customer>();
        var iterator = _container.GetItemQueryIterator<Models.Customer>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        _logger.LogInformation(
            "Retrieved {Count} customers with status {Status}",
            results.Count, status);

        return results;
    }
}
