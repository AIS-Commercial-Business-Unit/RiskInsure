using System.Text.Json.Serialization;

namespace RiskInsure.Billing.Domain.Services.BillingDb;

/// <summary>
/// Cosmos DB document model for BillingAccount persistence.
/// Note: 'id' field is REQUIRED by Cosmos DB and must be unique within the partition.
/// We use accountId as both the business identifier and the Cosmos DB id.
/// </summary>
public class BillingAccountDocument
{
    /// <summary>
    /// Cosmos DB required document identifier.
    /// Set to same value as AccountId for billing accounts.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Business identifier for the billing account.
    /// Also used as the partition key value.
    /// </summary>
    [JsonPropertyName("accountId")]
    public required string AccountId { get; set; }

    /// <summary>
    /// Document type discriminator for queries.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "BillingAccount";

    [JsonPropertyName("customerId")]
    public required string CustomerId { get; set; }

    [JsonPropertyName("policyNumber")]
    public required string PolicyNumber { get; set; }

    [JsonPropertyName("policyHolderName")]
    public string? PolicyHolderName { get; set; }

    [JsonPropertyName("currentPremiumOwed")]
    public decimal CurrentPremiumOwed { get; set; }

    [JsonPropertyName("totalPremiumDue")]
    public decimal TotalPremiumDue { get; set; }

    [JsonPropertyName("totalPaid")]
    public decimal TotalPaid { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("billingCycle")]
    public string? BillingCycle { get; set; }

    [JsonPropertyName("effectiveDate")]
    public DateTimeOffset EffectiveDate { get; set; }

    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; set; }

    [JsonPropertyName("lastUpdatedUtc")]
    public DateTimeOffset LastUpdatedUtc { get; set; }

    /// <summary>
    /// Cosmos DB ETag for optimistic concurrency control.
    /// Automatically populated by Cosmos DB on read operations.
    /// </summary>
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
