namespace RiskInsure.Billing.Domain.Contracts.Commands;

/// <summary>
/// Command to record a payment to a billing account
/// </summary>
public record RecordPayment(
    /// <summary>
    /// Unique message identifier
    /// </summary>
    Guid MessageId,
    
    /// <summary>
    /// When the command occurred
    /// </summary>
    DateTimeOffset OccurredUtc,
    
    /// <summary>
    /// Billing account identifier
    /// </summary>
    string AccountId,
    
    /// <summary>
    /// Payment amount to apply
    /// </summary>
    decimal Amount,
    
    /// <summary>
    /// Reference number from payment source (e.g., ACH trace number)
    /// </summary>
    string ReferenceNumber,
    
    /// <summary>
    /// Idempotency key for duplicate detection
    /// </summary>
    string IdempotencyKey
);
