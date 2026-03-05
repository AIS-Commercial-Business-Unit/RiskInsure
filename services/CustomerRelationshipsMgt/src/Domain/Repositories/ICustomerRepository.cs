namespace RiskInsure.Customer.Domain.Repositories;

using RiskInsure.Customer.Domain.Models;

public interface ICustomerRepository
{
    Task<Models.Customer?> GetByIdAsync(string customerId);
    Task<Models.Customer?> GetByEmailAsync(string email);
    Task<Models.Customer> CreateAsync(Models.Customer customer);
    Task<Models.Customer> UpdateAsync(Models.Customer customer);
    Task DeleteAsync(string customerId);
    Task<IEnumerable<Models.Customer>> GetByStatusAsync(string status);
}
