namespace RiskInsure.NsbShipping.Domain.Models;

using System.Text.Json.Serialization;

public class InventoryReservation
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonPropertyName("orderId")]
    public Guid OrderId { get; set; }

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "InventoryReservation";

    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; set; }

    [JsonPropertyName("updatedUtc")]
    public DateTimeOffset UpdatedUtc { get; set; }

    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
