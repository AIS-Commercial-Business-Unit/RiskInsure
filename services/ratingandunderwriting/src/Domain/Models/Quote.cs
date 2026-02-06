namespace RiskInsure.RatingAndUnderwriting.Domain.Models;

using System.Text.Json.Serialization;

public class Quote
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("quoteId")]
    public string QuoteId { get; set; } = string.Empty;

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "Quote";

    // Customer Reference
    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    // Quote Metadata
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Draft";

    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; set; }

    [JsonPropertyName("expirationUtc")]
    public DateTimeOffset ExpirationUtc { get; set; }

    [JsonPropertyName("acceptedUtc")]
    public DateTimeOffset? AcceptedUtc { get; set; }

    // Coverage Selections
    [JsonPropertyName("structureCoverageLimit")]
    public decimal StructureCoverageLimit { get; set; }

    [JsonPropertyName("structureDeductible")]
    public decimal StructureDeductible { get; set; }

    [JsonPropertyName("contentsCoverageLimit")]
    public decimal ContentsCoverageLimit { get; set; }

    [JsonPropertyName("contentsDeductible")]
    public decimal ContentsDeductible { get; set; }

    [JsonPropertyName("termMonths")]
    public int TermMonths { get; set; }

    [JsonPropertyName("effectiveDate")]
    public DateTimeOffset EffectiveDate { get; set; }

    // Underwriting Information
    [JsonPropertyName("priorClaimsCount")]
    public int? PriorClaimsCount { get; set; }

    [JsonPropertyName("kwegiboAge")]
    public int? KwegiboAge { get; set; }

    [JsonPropertyName("creditTier")]
    public string? CreditTier { get; set; }

    [JsonPropertyName("underwritingClass")]
    public string? UnderwritingClass { get; set; }

    [JsonPropertyName("declineReason")]
    public string? DeclineReason { get; set; }

    // Rating Information
    [JsonPropertyName("premium")]
    public decimal? Premium { get; set; }

    [JsonPropertyName("baseRate")]
    public decimal? BaseRate { get; set; }

    [JsonPropertyName("coverageFactor")]
    public decimal? CoverageFactor { get; set; }

    [JsonPropertyName("termFactor")]
    public decimal? TermFactor { get; set; }

    [JsonPropertyName("ageFactor")]
    public decimal? AgeFactor { get; set; }

    [JsonPropertyName("territoryFactor")]
    public decimal? TerritoryFactor { get; set; }

    [JsonPropertyName("zipCode")]
    public string? ZipCode { get; set; }

    // Audit
    [JsonPropertyName("updatedUtc")]
    public DateTimeOffset UpdatedUtc { get; set; }

    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
