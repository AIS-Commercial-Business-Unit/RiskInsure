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
}
