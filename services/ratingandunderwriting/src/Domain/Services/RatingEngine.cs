namespace RiskInsure.RatingAndUnderwriting.Domain.Services;

public record RatingBreakdown(
    decimal BaseRate,
    decimal CoverageFactor,
    decimal TermFactor,
    decimal AgeFactor,
    decimal TerritoryFactor,
    decimal Premium
);

public interface IRatingEngine
{
    decimal CalculatePremium(Models.Quote quote, string zipCode);
    RatingBreakdown GetRatingBreakdown(Models.Quote quote, string zipCode);
    decimal GetTerritoryFactor(string zipCode);
    decimal GetAgeFactor(int kwegiboAge);
    decimal GetTermFactor(int termMonths);
    decimal GetCoverageFactor(decimal structureLimit, decimal contentsLimit);
}

public class RatingEngine : IRatingEngine
{
    private const decimal BASE_RATE = 500m;

    public decimal CalculatePremium(Models.Quote quote, string zipCode)
    {
        var coverageFactor = GetCoverageFactor(quote.StructureCoverageLimit, quote.ContentsCoverageLimit);
        var termFactor = GetTermFactor(quote.TermMonths);
        var ageFactor = GetAgeFactor(quote.KwegiboAge ?? 0);
        var territoryFactor = GetTerritoryFactor(zipCode);

        var premium = BASE_RATE * coverageFactor * termFactor * ageFactor * territoryFactor;

        return Math.Round(premium, 2);
    }

    public RatingBreakdown GetRatingBreakdown(Models.Quote quote, string zipCode)
    {
        var coverageFactor = GetCoverageFactor(quote.StructureCoverageLimit, quote.ContentsCoverageLimit);
        var termFactor = GetTermFactor(quote.TermMonths);
        var ageFactor = GetAgeFactor(quote.KwegiboAge ?? 0);
        var territoryFactor = GetTerritoryFactor(zipCode);

        var premium = BASE_RATE * coverageFactor * termFactor * ageFactor * territoryFactor;

        return new RatingBreakdown(
            BASE_RATE,
            coverageFactor,
            termFactor,
            ageFactor,
            territoryFactor,
            Math.Round(premium, 2)
        );
    }

    public decimal GetCoverageFactor(decimal structureLimit, decimal contentsLimit)
    {
        var structureFactor = structureLimit / 100000m;
        var contentsFactor = contentsLimit / 50000m;
        return structureFactor + contentsFactor;
    }

    public decimal GetTermFactor(int termMonths)
    {
        return termMonths == 6 ? 0.55m : 1.00m;
    }

    public decimal GetAgeFactor(int kwegiboAge)
    {
        return kwegiboAge switch
        {
            <= 5 => 0.80m,
            <= 15 => 1.00m,
            <= 30 => 1.20m,
            _ => 1.50m
        };
    }

    public decimal GetTerritoryFactor(string zipCode)
    {
        return zipCode switch
        {
            "90210" or "10001" => 0.90m,  // Zone 1
            "60601" or "33101" => 1.00m,  // Zone 2
            "70112" or "94102" => 1.20m,  // Zone 3
            _ => 1.10m  // Zone 4 (default)
        };
    }
}
