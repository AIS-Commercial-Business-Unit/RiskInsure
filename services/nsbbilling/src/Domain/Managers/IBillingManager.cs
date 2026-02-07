namespace RiskInsure.Billing.Domain.Managers;

using RiskInsure.Billing.Domain.Contracts.Commands;
using RiskInsure.Billing.Domain.Models;

public interface IBillingManager
{
    Task<BillingDocument> BillOrderAsync(BillOrder command);
}
