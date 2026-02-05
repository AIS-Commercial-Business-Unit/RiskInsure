using System.Text.Json.Serialization;

namespace RiskInsure.FundTransferMgt.Domain.Models;

/// <summary>
/// Transfer direction
/// </summary>
public enum TransferDirection
{
    Inbound,  // Money coming in (from customer)
    Outbound  // Money going out (to customer)
}

/// <summary>
/// Transfer status
/// </summary>
public enum TransferStatus
{
    Pending,
    Authorizing,
    Authorized,
    Settling,
    Settled,
    Failed,
    Reversed
}

/// <summary>
/// Fund transfer aggregate - represents a single money movement transaction
/// Document stored in Transactions container with partition key /transactionId
/// </summary>
public class FundTransfer
{
    /// <summary>
    /// Cosmos DB required id field - maps to TransactionId
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Unique transaction identifier (also partition key)
    /// </summary>
    public string TransactionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Customer identifier (denormalized for lookups)
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Payment method used for this transfer
    /// </summary>
    public string PaymentMethodId { get; set; } = string.Empty;
    
    /// <summary>
    /// Transfer amount
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Direction of transfer (Inbound/Outbound)
    /// </summary>
    public TransferDirection Direction { get; set; }
    
    /// <summary>
    /// Current status
    /// </summary>
    public TransferStatus Status { get; set; }
    
    /// <summary>
    /// Purpose of transfer (e.g., PREMIUM_PAYMENT, REFUND)
    /// </summary>
    public string Purpose { get; set; } = string.Empty;
    
    /// <summary>
    /// External gateway transaction reference
    /// </summary>
    public string? GatewayTransactionId { get; set; }
    
    /// <summary>
    /// Failure reason if status is Failed
    /// </summary>
    public string? FailureReason { get; set; }
    
    /// <summary>
    /// Error code if status is Failed
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// When transfer was initiated
    /// </summary>
    public DateTimeOffset InitiatedUtc { get; set; }
    
    /// <summary>
    /// When transfer was authorized
    /// </summary>
    public DateTimeOffset? AuthorizedUtc { get; set; }
    
    /// <summary>
    /// When transfer was settled
    /// </summary>
    public DateTimeOffset? SettledUtc { get; set; }
    
    /// <summary>
    /// When transfer failed
    /// </summary>
    public DateTimeOffset? FailedUtc { get; set; }
    
    /// <summary>
    /// Cosmos DB document type discriminator
    /// </summary>
    public string Type_Discriminator { get; set; } = "FundTransfer";
}

/// <summary>
/// Refund aggregate - represents a refund transaction
/// Document stored in Transactions container with partition key /transactionId
/// </summary>
public class Refund
{
    /// <summary>
    /// Cosmos DB required id field - maps to RefundId
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Unique refund identifier (transaction ID for this refund)
    /// </summary>
    public string RefundId { get; set; } = string.Empty;
    
    /// <summary>
    /// Customer identifier (denormalized)
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Original transaction being refunded
    /// </summary>
    public string OriginalTransactionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Payment method receiving the refund
    /// </summary>
    public string PaymentMethodId { get; set; } = string.Empty;
    
    /// <summary>
    /// Refund amount
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Current status
    /// </summary>
    public TransferStatus Status { get; set; }
    
    /// <summary>
    /// Reason for refund
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// External gateway refund reference
    /// </summary>
    public string? GatewayRefundId { get; set; }
    
    /// <summary>
    /// When refund was initiated
    /// </summary>
    public DateTimeOffset InitiatedUtc { get; set; }
    
    /// <summary>
    /// When refund was processed
    /// </summary>
    public DateTimeOffset? ProcessedUtc { get; set; }
    
    /// <summary>
    /// When refund failed
    /// </summary>
    public DateTimeOffset? FailedUtc { get; set; }
    
    /// <summary>
    /// Failure reason if status is Failed
    /// </summary>
    public string? FailureReason { get; set; }
    
    /// <summary>
    /// Cosmos DB document type discriminator
    /// </summary>
    public string Type_Discriminator { get; set; } = "Refund";
}
