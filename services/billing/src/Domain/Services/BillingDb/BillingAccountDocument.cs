using System.Text.Json.Serialization;

namespace RiskInsure.Billing.Domain.Services.BillingDb;

/// <summary>
/// Cosmos DB document model for BillingAccount persistence.
/// </summary>
public class BillingAccountDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "BillingAccount";

    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonPropertyName("policyNumber")]
    public string PolicyNumber { get; set; } = string.Empty;

    [JsonPropertyName("totalPremiumDue")]
    public decimal TotalPremiumDue { get; set; }

    [JsonPropertyName("totalPaid")]
    public decimal TotalPaid { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; set; }

    [JsonPropertyName("lastUpdatedUtc")]
    public DateTimeOffset LastUpdatedUtc { get; set; }

    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
