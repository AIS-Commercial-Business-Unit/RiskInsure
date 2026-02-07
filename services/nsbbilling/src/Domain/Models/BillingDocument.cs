namespace RiskInsure.Billing.Domain.Models;

using System.Text.Json.Serialization;

public class BillingDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Billing";
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Charged";
    
    [JsonPropertyName("chargedAt")]
    public DateTimeOffset ChargedAt { get; set; }
    
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
    
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
    
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
