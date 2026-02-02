namespace RiskInsure.PublicContracts.Events;

/// <summary>
/// Event published when a payment has been received and applied to a billing account
/// </summary>
public record PaymentReceived(
    /// <summary>
    /// Unique message identifier
    /// </summary>
    Guid MessageId,
    
    /// <summary>
    /// When the event occurred
    /// </summary>
    DateTimeOffset OccurredUtc,
    
    /// <summary>
    /// Billing account identifier that received the payment
    /// </summary>
    string AccountId,
    
    /// <summary>
    /// Payment amount that was applied
    /// </summary>
    decimal Amount,
    
    /// <summary>
    /// Reference number from payment source (e.g., ACH trace number)
    /// </summary>
    string ReferenceNumber,
    
    /// <summary>
    /// Updated total paid amount after this payment
    /// </summary>
    decimal TotalPaid,
    
    /// <summary>
    /// Outstanding balance after this payment
    /// </summary>
    decimal OutstandingBalance,
    
    /// <summary>
    /// Idempotency key for duplicate detection
    /// </summary>
    string IdempotencyKey
);
