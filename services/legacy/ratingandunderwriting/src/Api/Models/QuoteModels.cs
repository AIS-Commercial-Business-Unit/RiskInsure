namespace RiskInsure.RatingAndUnderwriting.Api.Models;

using System.ComponentModel.DataAnnotations;

public class StartQuoteRequest
{
    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [Range(50000, 500000)]
    public decimal StructureCoverageLimit { get; set; }

    public decimal StructureDeductible { get; set; }

    [Range(10000, 150000)]
    public decimal? ContentsCoverageLimit { get; set; }

    public decimal? ContentsDeductible { get; set; }

    public int TermMonths { get; set; }

    [Required]
    public DateTimeOffset EffectiveDate { get; set; }
    
    [Required]
    public string PropertyZipCode { get; set; } = string.Empty;
}

public class StartQuoteResponse
{
    public string QuoteId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset ExpirationUtc { get; set; }
}

public class SubmitUnderwritingRequest
{
    [Range(0, int.MaxValue)]
    public int PriorClaimsCount { get; set; }

    [Range(0, 100)]
    public int PropertyAgeYears { get; set; }

    [Required]
    public string CreditTier { get; set; } = string.Empty;
}

public class SubmitUnderwritingResponse
{
    public string QuoteId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? UnderwritingClass { get; set; }
    public decimal? Premium { get; set; }
    public DateTimeOffset? ExpirationUtc { get; set; }
}

public class AcceptQuoteResponse
{
    public string QuoteId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset AcceptedUtc { get; set; }
    public decimal Premium { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool PolicyCreationInitiated { get; set; }
}

public class QuoteResponse
{
    public string QuoteId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal StructureCoverageLimit { get; set; }
    public decimal StructureDeductible { get; set; }
    public decimal ContentsCoverageLimit { get; set; }
    public decimal ContentsDeductible { get; set; }
    public int TermMonths { get; set; }
    public DateTimeOffset EffectiveDate { get; set; }
    public decimal? Premium { get; set; }
    public string? UnderwritingClass { get; set; }
    public DateTimeOffset ExpirationUtc { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}

public class CustomerQuotesResponse
{
    public string CustomerId { get; set; } = string.Empty;
    public List<QuoteSummary> Quotes { get; set; } = new();
}

public class QuoteSummary
{
    public string QuoteId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? Premium { get; set; }
    public DateTimeOffset ExpirationUtc { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}
