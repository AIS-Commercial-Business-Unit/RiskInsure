using Microsoft.Extensions.Logging;
using RiskInsure.FundTransferMgt.Domain.Models;
using RiskInsure.FundTransferMgt.Domain.Repositories;
using RiskInsure.FundTransferMgt.Domain.Services;

namespace RiskInsure.FundTransferMgt.Domain.Managers;

public class PaymentMethodManager : IPaymentMethodManager
{
    private readonly IPaymentMethodRepository _repository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ILogger<PaymentMethodManager> _logger;

    public PaymentMethodManager(
        IPaymentMethodRepository repository,
        IPaymentGateway paymentGateway,
        ILogger<PaymentMethodManager> logger)
    {
        _repository = repository;
        _paymentGateway = paymentGateway;
        _logger = logger;
    }

    public async Task<PaymentMethod> AddCreditCardAsync(
        string paymentMethodId,
        string customerId,
        string cardholderName,
        string cardNumber,
        int expirationMonth,
        int expirationYear,
        string cvv,
        Address billingAddress,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Adding credit card for customer {CustomerId} with PaymentMethodId {PaymentMethodId}",
            customerId, paymentMethodId);

        // Validate input
        if (string.IsNullOrWhiteSpace(cardholderName))
        {
            throw new ArgumentException("Cardholder name cannot be empty", nameof(cardholderName));
        }

        // Check for duplicate PaymentMethodId
        var existing = await _repository.GetByIdAsync(paymentMethodId, cancellationToken);
        if (existing != null)
        {
            _logger.LogWarning(
                "Payment method {PaymentMethodId} already exists - returning existing record",
                paymentMethodId);
            return existing;
        }

        // Validate card
        ValidateCreditCard(cardNumber, expirationMonth, expirationYear);

        // Tokenize card through gateway
        string token;
        try
        {
            token = await _paymentGateway.TokenizeCardAsync(
                cardNumber, expirationMonth, expirationYear, cvv, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to tokenize card for customer {CustomerId}", customerId);
            throw new InvalidOperationException("Failed to tokenize card", ex);
        }

        var paymentMethod = new PaymentMethod
        {
            PaymentMethodId = paymentMethodId,
            CustomerId = customerId,
            Type = PaymentMethodType.CreditCard,
            Status = PaymentMethodStatus.Validated,
            Card = new CardDetails
            {
                CardholderName = cardholderName,
                Last4 = cardNumber.Substring(cardNumber.Length - 4),
                Brand = DetermineCardBrand(cardNumber),
                ExpirationMonth = expirationMonth,
                ExpirationYear = expirationYear,
                Token = token,
                BillingAddress = billingAddress
            }
        };

        await _repository.CreateAsync(paymentMethod, cancellationToken);

        _logger.LogInformation(
            "Credit card added successfully - PaymentMethodId: {PaymentMethodId}",
            paymentMethod.PaymentMethodId);

        return paymentMethod;
    }

    public async Task<PaymentMethod> AddAchAccountAsync(
        string paymentMethodId,
        string customerId,
        string accountHolderName,
        string routingNumber,
        string accountNumber,
        string accountType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Adding ACH account for customer {CustomerId} with PaymentMethodId {PaymentMethodId}",
            customerId, paymentMethodId);

        // Check for duplicate PaymentMethodId
        var existing = await _repository.GetByIdAsync(paymentMethodId, cancellationToken);
        if (existing != null)
        {
            _logger.LogWarning(
                "Payment method {PaymentMethodId} already exists - returning existing record",
                paymentMethodId);
            return existing;
        }

        // Validate routing number
        if (!ValidateRoutingNumber(routingNumber))
        {
            throw new ArgumentException("Invalid routing number", nameof(routingNumber));
        }

        // Validate account number
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            throw new ArgumentException("Account number cannot be empty", nameof(accountNumber));
        }

        // Tokenize account through gateway
        string token;
        try
        {
            token = await _paymentGateway.TokenizeAchAccountAsync(
                routingNumber, accountNumber, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to tokenize ACH account for customer {CustomerId}", customerId);
            throw new InvalidOperationException("Failed to tokenize ACH account", ex);
        }

        var paymentMethod = new PaymentMethod
        {
            PaymentMethodId = paymentMethodId,
            CustomerId = customerId,
            Type = PaymentMethodType.Ach,
            Status = PaymentMethodStatus.Validated,
            Ach = new AchDetails
            {
                AccountHolderName = accountHolderName,
                RoutingNumber = routingNumber,
                Last4 = accountNumber.Substring(accountNumber.Length - 4),
                AccountType = accountType,
                EncryptedAccountNumber = token
            }
        };

        await _repository.CreateAsync(paymentMethod, cancellationToken);

        _logger.LogInformation(
            "ACH account added successfully - PaymentMethodId: {PaymentMethodId}",
            paymentMethod.PaymentMethodId);

        return paymentMethod;
    }

    public async Task<List<PaymentMethod>> GetCustomerPaymentMethodsAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetByCustomerIdAsync(customerId, cancellationToken);
    }

    public async Task<PaymentMethod?> GetPaymentMethodAsync(
        string paymentMethodId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(paymentMethodId, cancellationToken);
    }

    public async Task RemovePaymentMethodAsync(
        string paymentMethodId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Removing payment method {PaymentMethodId}", paymentMethodId);
        
        var paymentMethod = await _repository.GetByIdAsync(paymentMethodId, cancellationToken);
        if (paymentMethod == null)
        {
            throw new InvalidOperationException($"Payment method {paymentMethodId} not found");
        }

        paymentMethod.Status = PaymentMethodStatus.Inactive;
        await _repository.UpdateAsync(paymentMethod, cancellationToken);
    }

    private void ValidateCreditCard(string cardNumber, int expirationMonth, int expirationYear)
    {
        // Remove spaces and dashes
        cardNumber = cardNumber.Replace(" ", "").Replace("-", "");

        // Check length
        if (cardNumber.Length < 13 || cardNumber.Length > 19)
        {
            throw new ArgumentException("Invalid card number length", nameof(cardNumber));
        }

        // Luhn algorithm check
        if (!LuhnCheck(cardNumber))
        {
            throw new ArgumentException("Invalid card number (failed Luhn check)", nameof(cardNumber));
        }

        // Check expiration
        if (expirationMonth < 1 || expirationMonth > 12)
        {
            throw new ArgumentException("Invalid expiration month", nameof(expirationMonth));
        }

        var now = DateTimeOffset.UtcNow;
        var expiration = new DateTimeOffset(expirationYear, expirationMonth, 1, 0, 0, 0, TimeSpan.Zero)
            .AddMonths(1).AddDays(-1);

        if (expiration < now)
        {
            throw new ArgumentException("Card is expired", nameof(expirationYear));
        }
    }

    private bool LuhnCheck(string cardNumber)
    {
        int sum = 0;
        bool alternate = false;

        for (int i = cardNumber.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(cardNumber[i]))
                return false;

            int n = int.Parse(cardNumber[i].ToString());

            if (alternate)
            {
                n *= 2;
                if (n > 9)
                    n -= 9;
            }

            sum += n;
            alternate = !alternate;
        }

        return (sum % 10 == 0);
    }

    private string DetermineCardBrand(string cardNumber)
    {
        if (cardNumber.StartsWith("4"))
            return "Visa";
        if (cardNumber.StartsWith("5"))
            return "MasterCard";
        if (cardNumber.StartsWith("3"))
            return "AmericanExpress";
        if (cardNumber.StartsWith("6"))
            return "Discover";

        return "Unknown";
    }

    private bool ValidateRoutingNumber(string routingNumber)
    {
        if (routingNumber.Length != 9)
            return false;

        if (!routingNumber.All(char.IsDigit))
            return false;

        // ABA routing number checksum validation
        int[] digits = routingNumber.Select(c => int.Parse(c.ToString())).ToArray();
        int checksum = (3 * (digits[0] + digits[3] + digits[6]) +
                       7 * (digits[1] + digits[4] + digits[7]) +
                       (digits[2] + digits[5] + digits[8])) % 10;

        return checksum == 0;
    }
}
