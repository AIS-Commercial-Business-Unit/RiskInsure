namespace RiskInsure.PublicContracts.Events;

/// <summary>
/// Event published when refund is processed and returned to customer
/// </summary>
public record FundsRefunded(
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
    /// Refund identifier
    /// </summary>
    string RefundId,
    
    /// <summary>
    /// Original transaction being refunded
    /// </summary>
    string OriginalTransactionId,
    
    /// <summary>
    /// Refund amount
    /// </summary>
    decimal Amount,
    
    /// <summary>
    /// When refund was processed
    /// </summary>
    DateTimeOffset RefundedUtc,
    
    /// <summary>
    /// Business reason for refund
    /// </summary>
    string Reason,
    
    /// <summary>
    /// Idempotency key for duplicate detection
    /// </summary>
    string IdempotencyKey
);
