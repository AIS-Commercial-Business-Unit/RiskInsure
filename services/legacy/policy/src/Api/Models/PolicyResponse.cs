namespace RiskInsure.Policy.Api.Models;

public class PolicyResponse
{
    public required string PolicyId { get; set; }
    public required string PolicyNumber { get; set; }
    public required string CustomerId { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset EffectiveDate { get; set; }
    public DateTimeOffset ExpirationDate { get; set; }
    public decimal Premium { get; set; }
    public DateTimeOffset BoundDate { get; set; }
    public DateTimeOffset? IssuedDate { get; set; }
    public DateTimeOffset? CancelledDate { get; set; }
    public string? CancellationReason { get; set; }
    public decimal? UnearnedPremium { get; set; }
    public decimal StructureCoverageLimit { get; set; }
    public decimal StructureDeductible { get; set; }
    public decimal? ContentsCoverageLimit { get; set; }
    public decimal? ContentsDeductible { get; set; }
    public int TermMonths { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}
