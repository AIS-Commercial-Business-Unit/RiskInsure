namespace RiskInsure.Billing.Domain.Managers.DTOs;

/// <summary>
/// Result of a billing account operation
/// </summary>
public class BillingAccountResult
{
    /// <summary>
    /// Whether the operation succeeded
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// The account ID involved in the operation
    /// </summary>
    public string? AccountId { get; set; }
    
    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Error code for programmatic handling
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// Whether the operation is retryable
    /// </summary>
    public bool IsRetryable { get; set; }
    
    /// <summary>
    /// Factory method for success result
    /// </summary>
    public static BillingAccountResult Success(string accountId) => new()
    {
        IsSuccess = true,
        AccountId = accountId
    };
    
    /// <summary>
    /// Factory method for failure result
    /// </summary>
    public static BillingAccountResult Failure(string errorMessage, string errorCode, bool isRetryable = false) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage,
        ErrorCode = errorCode,
        IsRetryable = isRetryable
    };
}
