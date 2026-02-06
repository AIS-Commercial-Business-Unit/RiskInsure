namespace RiskInsure.Policy.Domain.Models;

using System.Text.Json.Serialization;

public class Policy
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    
    [JsonPropertyName("policyId")]
    public required string PolicyId { get; set; }  // Partition key
    
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "Policy";
    
    // Policy Identity
    [JsonPropertyName("policyNumber")]
    public required string PolicyNumber { get; set; }  // KWG-2026-000001
    
    [JsonPropertyName("quoteId")]
    public required string QuoteId { get; set; }
    
    [JsonPropertyName("customerId")]
    public required string CustomerId { get; set; }
    
    // Policy Status & Dates
    [JsonPropertyName("status")]
    public required string Status { get; set; }  // Bound, Issued, Active, Cancelled, Expired, Lapsed, Reinstated
    
    [JsonPropertyName("effectiveDate")]
    public DateTimeOffset EffectiveDate { get; set; }
    
    [JsonPropertyName("expirationDate")]
    public DateTimeOffset ExpirationDate { get; set; }
    
    [JsonPropertyName("boundDate")]
    public DateTimeOffset BoundDate { get; set; }
    
    [JsonPropertyName("issuedDate")]
    public DateTimeOffset? IssuedDate { get; set; }
    
    [JsonPropertyName("cancelledDate")]
    public DateTimeOffset? CancelledDate { get; set; }
    
    // Coverage Details (from accepted quote)
    [JsonPropertyName("structureCoverageLimit")]
    public decimal StructureCoverageLimit { get; set; }
    
    [JsonPropertyName("structureDeductible")]
    public decimal StructureDeductible { get; set; }
    
    [JsonPropertyName("contentsCoverageLimit")]
    public decimal ContentsCoverageLimit { get; set; }
    
    [JsonPropertyName("contentsDeductible")]
    public decimal ContentsDeductible { get; set; }
    
    [JsonPropertyName("termMonths")]
    public int TermMonths { get; set; }  // 6 or 12
    
    // Premium Information
    [JsonPropertyName("premium")]
    public decimal Premium { get; set; }
    
    // Cancellation Information
    [JsonPropertyName("cancellationReason")]
    public string? CancellationReason { get; set; }
    
    [JsonPropertyName("unearnedPremium")]
    public decimal? UnearnedPremium { get; set; }
    
    // Audit
    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; set; }
    
    [JsonPropertyName("updatedUtc")]
    public DateTimeOffset UpdatedUtc { get; set; }
    
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
