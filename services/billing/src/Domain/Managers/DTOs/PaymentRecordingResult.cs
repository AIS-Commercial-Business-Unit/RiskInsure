namespace RiskInsure.Billing.Domain.Managers.DTOs;

using RiskInsure.Billing.Domain.Models;

/// <summary>
/// Result of a payment recording operation.
/// Indicates success/failure with detailed information.
/// </summary>
public class PaymentRecordingResult
{
    /// <summary>
    /// Whether the payment was successfully recorded
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code for programmatic error handling
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Whether the error is retryable (transient failure)
    /// </summary>
    public bool IsRetryable { get; set; }

    /// <summary>
    /// Updated billing account after payment recording (if successful)
    /// </summary>
    public BillingAccount? UpdatedAccount { get; set; }

    /// <summary>
    /// Indicates if this was a duplicate payment (idempotency check)
    /// </summary>
    public bool WasDuplicate { get; set; }

    /// <summary>
    /// Factory method for successful result
    /// </summary>
    public static PaymentRecordingResult Success(BillingAccount account, bool wasDuplicate = false)
    {
        return new PaymentRecordingResult
        {
            IsSuccess = true,
            UpdatedAccount = account,
            WasDuplicate = wasDuplicate
        };
    }

    /// <summary>
    /// Factory method for failure result
    /// </summary>
    public static PaymentRecordingResult Failure(string errorMessage, string errorCode, bool isRetryable = false)
    {
        return new PaymentRecordingResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            IsRetryable = isRetryable
        };
    }
}
