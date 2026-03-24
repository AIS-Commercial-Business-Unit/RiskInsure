namespace RiskInsure.RatingAndUnderwriting.Domain.Services;

public record UnderwritingResult(
    bool IsApproved,
    string? UnderwritingClass,
    string? DeclineReason
);

public record UnderwritingSubmission(
    int PriorClaimsCount,
    int KwegiboAge,
    string CreditTier
);

public interface IUnderwritingEngine
{
    UnderwritingResult Evaluate(UnderwritingSubmission submission);
    bool IsRiskAcceptable(int priorClaimsCount, int kwegiboAge, string creditTier);
    string? DetermineUnderwritingClass(UnderwritingSubmission submission);
}

public class UnderwritingEngine : IUnderwritingEngine
{
    public UnderwritingResult Evaluate(UnderwritingSubmission submission)
    {
        // Class A (Preferred)
        if (submission.PriorClaimsCount == 0 && 
            submission.KwegiboAge <= 15 && 
            submission.CreditTier == "Excellent")
        {
            return new UnderwritingResult(true, "A", null);
        }

        // Class B (Standard)
        if (submission.PriorClaimsCount <= 1 && 
            submission.KwegiboAge <= 30 && 
            (submission.CreditTier == "Good" || submission.CreditTier == "Excellent"))
        {
            return new UnderwritingResult(true, "B", null);
        }

        // Declined
        if (submission.PriorClaimsCount >= 3)
        {
            return new UnderwritingResult(false, null, "Excessive prior claims (3 or more in past 3 years)");
        }

        if (submission.KwegiboAge > 30)
        {
            return new UnderwritingResult(false, null, "Kwegibo age exceeds maximum allowable (30 years)");
        }

        if (submission.CreditTier == "Poor")
        {
            return new UnderwritingResult(false, null, "Credit tier does not meet minimum requirements");
        }

        return new UnderwritingResult(false, null, "Does not meet underwriting criteria");
    }

    public bool IsRiskAcceptable(int priorClaimsCount, int kwegiboAge, string creditTier)
    {
        var submission = new UnderwritingSubmission(priorClaimsCount, kwegiboAge, creditTier);
        return Evaluate(submission).IsApproved;
    }

    public string? DetermineUnderwritingClass(UnderwritingSubmission submission)
    {
        return Evaluate(submission).UnderwritingClass;
    }
}
