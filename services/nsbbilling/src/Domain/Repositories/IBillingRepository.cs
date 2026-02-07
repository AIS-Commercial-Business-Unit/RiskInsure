namespace RiskInsure.Billing.Domain.Repositories;

using RiskInsure.Billing.Domain.Models;

public interface IBillingRepository
{
    Task<BillingDocument?> GetByOrderIdAsync(Guid orderId);
    Task<BillingDocument> CreateAsync(BillingDocument billing);
    Task<BillingDocument> UpdateAsync(BillingDocument billing);
}
