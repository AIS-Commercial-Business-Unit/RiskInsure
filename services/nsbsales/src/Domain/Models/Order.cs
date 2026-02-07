namespace RiskInsure.NsbSales.Domain.Models;

using System.Text.Json.Serialization;

public class Order
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Order";
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Placed";
    
    [JsonPropertyName("placedAt")]
    public DateTimeOffset PlacedAt { get; set; }
    
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
    
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
    
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
