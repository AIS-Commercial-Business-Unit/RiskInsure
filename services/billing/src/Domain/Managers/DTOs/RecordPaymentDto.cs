namespace RiskInsure.Billing.Domain.Managers.DTOs;

/// <summary>
/// DTO for recording a payment to a billing account.
/// Used by both API and Event Handlers to call the Manager.
/// </summary>
public class RecordPaymentDto
{
    /// <summary>
    /// Billing account identifier
    /// </summary>
    public required string AccountId { get; init; }

    /// <summary>
    /// Payment amount to apply
    /// </summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// Reference number from payment source (e.g., ACH trace number, check number)
    /// </summary>
    public required string ReferenceNumber { get; init; }

    /// <summary>
    /// Idempotency key for duplicate detection
    /// </summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>
    /// When the payment occurred (for auditing)
    /// </summary>
    public DateTimeOffset OccurredUtc { get; init; } = DateTimeOffset.UtcNow;
}
