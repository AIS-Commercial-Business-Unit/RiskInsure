namespace RiskInsure.PublicContracts.Events;

/// <summary>
/// Event published when customer-initiated payment is successfully authorized and settled
/// </summary>
public record FundsSettled(
    /// <summary>
    /// Unique message identifier
    /// </summary>
    Guid MessageId,
    
    /// <summary>
    /// When the event occurred
    /// </summary>
    DateTimeOffset OccurredUtc,
    
    /// <summary>
    /// Customer identifier
    /// </summary>
    string CustomerId,
    
    /// <summary>
    /// Transaction identifier from fund transfer system
    /// </summary>
    string TransactionId,
    
    /// <summary>
    /// Amount settled
    /// </summary>
    decimal Amount,
    
    /// <summary>
    /// Payment method used for settlement
    /// </summary>
    string PaymentMethodId,
    
    /// <summary>
    /// When funds were settled
    /// </summary>
    DateTimeOffset SettledUtc,
    
    /// <summary>
    /// Idempotency key for duplicate detection
    /// </summary>
    string IdempotencyKey
);
