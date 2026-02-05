namespace RiskInsure.Billing.Domain.Managers;

using RiskInsure.Billing.Domain.Managers.DTOs;

/// <summary>
/// Manager interface for billing payment operations.
/// Defines business capabilities for recording and managing payments.
/// </summary>
public interface IBillingPaymentManager
{
    /// <summary>
    /// Records a payment to a billing account.
    /// Validates business rules, updates account, and publishes events.
    /// </summary>
    /// <param name="dto">Payment recording details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success/failure with details</returns>
    Task<PaymentRecordingResult> RecordPaymentAsync(
        RecordPaymentDto dto,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Applies settled funds from FundTransferMgt to billing account.
    /// Creates payment record using transaction ID as reference.
    /// </summary>
    Task ApplySettledFundsAsync(
        string customerId,
        string transactionId,
        decimal amount,
        DateTimeOffset settledUtc,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Applies refund by reversing payment application.
    /// Increases outstanding balance for refunded amount.
    /// </summary>
    Task ApplyRefundAsync(
        string customerId,
        string refundId,
        string originalTransactionId,
        decimal amount,
        DateTimeOffset refundedUtc,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
