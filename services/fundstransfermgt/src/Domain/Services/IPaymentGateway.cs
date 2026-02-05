namespace RiskInsure.FundTransferMgt.Domain.Services;

/// <summary>
/// Payment gateway authorization result
/// </summary>
public class AuthorizationResult
{
    public bool IsSuccess { get; set; }
    public string? GatewayTransactionId { get; set; }
    public string? FailureReason { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset ProcessedUtc { get; set; }
}

/// <summary>
/// Payment gateway refund result
/// </summary>
public class RefundResult
{
    public bool IsSuccess { get; set; }
    public string? GatewayRefundId { get; set; }
    public string? FailureReason { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset ProcessedUtc { get; set; }
}

/// <summary>
/// Interface for external payment gateway integration
/// Allows swapping mock implementation with real gateway (Stripe, Braintree, etc.)
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Authorize a payment using credit/debit card
    /// </summary>
    Task<AuthorizationResult> AuthorizeCardPaymentAsync(
        string cardToken,
        decimal amount,
        string customerId,
        string transactionId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Authorize a payment using ACH
    /// </summary>
    Task<AuthorizationResult> AuthorizeAchPaymentAsync(
        string accountToken,
        decimal amount,
        string customerId,
        string transactionId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Authorize a payment using digital wallet
    /// </summary>
    Task<AuthorizationResult> AuthorizeWalletPaymentAsync(
        string walletToken,
        decimal amount,
        string customerId,
        string transactionId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process a refund to original payment method
    /// </summary>
    Task<RefundResult> ProcessRefundAsync(
        string originalGatewayTransactionId,
        decimal amount,
        string customerId,
        string refundId,
        string reason,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Tokenize a credit card
    /// </summary>
    Task<string> TokenizeCardAsync(
        string cardNumber,
        int expirationMonth,
        int expirationYear,
        string cvv,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Tokenize an ACH account
    /// </summary>
    Task<string> TokenizeAchAccountAsync(
        string routingNumber,
        string accountNumber,
        CancellationToken cancellationToken = default);
}
