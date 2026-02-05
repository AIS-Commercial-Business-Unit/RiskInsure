using System.Text.Json.Serialization;

namespace RiskInsure.FundTransferMgt.Domain.Models;

/// <summary>
/// Payment method types supported
/// </summary>
public enum PaymentMethodType
{
    CreditCard,
    DebitCard,
    Ach,
    Wallet
}

/// <summary>
/// Payment method status
/// </summary>
public enum PaymentMethodStatus
{
    Pending,
    Validated,
    Active,
    Expired,
    Revoked,
    Inactive
}

/// <summary>
/// Payment method aggregate - represents customer's stored payment instrument
/// Document stored in PaymentMethods container with partition key /paymentMethodId
/// </summary>
public class PaymentMethod
{
    /// <summary>
    /// Cosmos DB required id field - maps to PaymentMethodId
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Unique payment method identifier (also partition key)
    /// </summary>
    public string PaymentMethodId { get; set; } = string.Empty;
    
    /// <summary>
    /// Customer identifier (denormalized for lookups)
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of payment method
    /// </summary>
    public PaymentMethodType Type { get; set; }
    
    /// <summary>
    /// Current status
    /// </summary>
    public PaymentMethodStatus Status { get; set; }
    
    /// <summary>
    /// Encrypted/tokenized card details (for credit/debit cards)
    /// </summary>
    public CardDetails? Card { get; set; }
    
    /// <summary>
    /// ACH account details (for ACH payments)
    /// </summary>
    public AchDetails? Ach { get; set; }
    
    /// <summary>
    /// Wallet details (for digital wallets)
    /// </summary>
    public WalletDetails? Wallet { get; set; }
    
    /// <summary>
    /// When the payment method was added
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
    
    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; set; }
    
    /// <summary>
    /// Cosmos DB document type discriminator
    /// </summary>
    public string Type_Discriminator { get; set; } = "PaymentMethod";
}

/// <summary>
/// Credit/Debit card details (tokenized)
/// </summary>
public class CardDetails
{
    /// <summary>
    /// Cardholder name
    /// </summary>
    public string CardholderName { get; set; } = string.Empty;
    
    /// <summary>
    /// Last 4 digits of card (for display)
    /// </summary>
    public string Last4 { get; set; } = string.Empty;
    
    /// <summary>
    /// Card brand (Visa, MasterCard, etc.)
    /// </summary>
    public string Brand { get; set; } = string.Empty;
    
    /// <summary>
    /// Expiration month (1-12)
    /// </summary>
    public int ExpirationMonth { get; set; }
    
    /// <summary>
    /// Expiration year (4 digits)
    /// </summary>
    public int ExpirationYear { get; set; }
    
    /// <summary>
    /// Tokenized card number (from payment gateway)
    /// </summary>
    public string Token { get; set; } = string.Empty;
    
    /// <summary>
    /// Billing address
    /// </summary>
    public Address? BillingAddress { get; set; }
}

/// <summary>
/// ACH account details (encrypted)
/// </summary>
public class AchDetails
{
    /// <summary>
    /// Account holder name
    /// </summary>
    public string AccountHolderName { get; set; } = string.Empty;
    
    /// <summary>
    /// Routing number
    /// </summary>
    public string RoutingNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Last 4 digits of account (for display)
    /// </summary>
    public string Last4 { get; set; } = string.Empty;
    
    /// <summary>
    /// Account type (Checking, Savings)
    /// </summary>
    public string AccountType { get; set; } = string.Empty;
    
    /// <summary>
    /// Encrypted account number
    /// </summary>
    public string EncryptedAccountNumber { get; set; } = string.Empty;
}

/// <summary>
/// Digital wallet details
/// </summary>
public class WalletDetails
{
    /// <summary>
    /// Wallet provider (ApplePay, GooglePay, etc.)
    /// </summary>
    public string Provider { get; set; } = string.Empty;
    
    /// <summary>
    /// Wallet token
    /// </summary>
    public string Token { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Billing address
/// </summary>
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
