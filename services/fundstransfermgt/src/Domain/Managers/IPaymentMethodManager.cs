using RiskInsure.FundTransferMgt.Domain.Models;

namespace RiskInsure.FundTransferMgt.Domain.Managers;

public interface IPaymentMethodManager
{
    Task<PaymentMethod> AddCreditCardAsync(
        string paymentMethodId,
        string customerId,
        string cardholderName,
        string cardNumber,
        int expirationMonth,
        int expirationYear,
        string cvv,
        Address billingAddress,
        CancellationToken cancellationToken = default);
    
    Task<PaymentMethod> AddAchAccountAsync(
        string paymentMethodId,
        string customerId,
        string accountHolderName,
        string routingNumber,
        string accountNumber,
        string accountType,
        CancellationToken cancellationToken = default);
    
    Task<List<PaymentMethod>> GetCustomerPaymentMethodsAsync(
        string customerId,
        CancellationToken cancellationToken = default);
    
    Task<PaymentMethod?> GetPaymentMethodAsync(
        string paymentMethodId,
        CancellationToken cancellationToken = default);
    
    Task RemovePaymentMethodAsync(
        string paymentMethodId,
        CancellationToken cancellationToken = default);
}
