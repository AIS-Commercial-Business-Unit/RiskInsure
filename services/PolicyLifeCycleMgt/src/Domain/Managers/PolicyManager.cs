namespace RiskInsure.Policy.Domain.Managers;

using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.Policy.Domain.Contracts.Events;
using RiskInsure.Policy.Domain.Repositories;
using RiskInsure.Policy.Domain.Services;
using RiskInsure.PublicContracts.Events;

public class PolicyManager : IPolicyManager
{
    private readonly IPolicyRepository _repository;
    private readonly IPolicyNumberGenerator _policyNumberGenerator;
    private readonly IMessageSession _messageSession;
    private readonly ILogger<PolicyManager> _logger;

    public PolicyManager(
        IPolicyRepository repository,
        IPolicyNumberGenerator policyNumberGenerator,
        IMessageSession messageSession,
        ILogger<PolicyManager> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _policyNumberGenerator = policyNumberGenerator ?? throw new ArgumentNullException(nameof(policyNumberGenerator));
        _messageSession = messageSession ?? throw new ArgumentNullException(nameof(messageSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Models.Policy> CreateFromQuoteAsync(QuoteAccepted quote)
    {
        _logger.LogInformation(
            "CreateFromQuoteAsync called for QuoteId {QuoteId}",
            quote.QuoteId);

        // Idempotency check
        var existing = await _repository.GetByQuoteIdAsync(quote.QuoteId);
        if (existing != null)
        {
            _logger.LogInformation(
                "Policy already exists for QuoteId {QuoteId}, returning existing policy {PolicyNumber}",
                quote.QuoteId, existing.PolicyNumber);
            return existing;
        }

        // Generate policy number and ID
        _logger.LogInformation("Generating policy number...");
        var policyNumber = await _policyNumberGenerator.GenerateAsync();
        _logger.LogInformation("Policy number generated: {PolicyNumber}", policyNumber);

        var policyId = Guid.NewGuid().ToString();
        _logger.LogInformation(
            "Creating policy with Id={PolicyId}, PolicyId={PolicyId}",
            policyId, policyId);

        // Create policy
        var policy = new Models.Policy
        {
            Id = policyId,           // Cosmos DB id
            PolicyId = policyId,     // Partition key (must match Id)
            PolicyNumber = policyNumber,
            QuoteId = quote.QuoteId,
            CustomerId = quote.CustomerId,
            Status = "Bound",
            EffectiveDate = quote.EffectiveDate,
            ExpirationDate = quote.EffectiveDate.AddMonths(quote.TermMonths),
            BoundDate = DateTimeOffset.UtcNow,
            StructureCoverageLimit = quote.StructureCoverageLimit,
            StructureDeductible = quote.StructureDeductible,
            ContentsCoverageLimit = quote.ContentsCoverageLimit,
            ContentsDeductible = quote.ContentsDeductible,
            TermMonths = quote.TermMonths,
            Premium = quote.Premium,
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };

        policy = await _repository.CreateAsync(policy);

        // Publish PolicyBound event
        await _messageSession.Publish(new PolicyBound(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            PolicyId: policy.PolicyId,
            PolicyNumber: policy.PolicyNumber,
            QuoteId: policy.QuoteId,
            CustomerId: policy.CustomerId,
            Premium: policy.Premium,
            EffectiveDate: policy.EffectiveDate,
            ExpirationDate: policy.ExpirationDate,
            IdempotencyKey: quote.IdempotencyKey
        ));

        _logger.LogInformation(
            "Policy {PolicyNumber} bound for Quote {QuoteId}",
            policyNumber, quote.QuoteId);

        return policy;
    }

    public async Task<Models.Policy> IssuePolicyAsync(string policyId)
    {
        var policy = await _repository.GetByIdAsync(policyId);
        if (policy == null)
        {
            throw new InvalidOperationException($"Policy {policyId} not found");
        }

        if (policy.Status != "Bound")
        {
            throw new InvalidOperationException($"Policy must be in 'Bound' status to issue. Current status: {policy.Status}");
        }

        if (policy.EffectiveDate < DateTimeOffset.UtcNow.Date)
        {
            throw new InvalidOperationException("Cannot issue policy with effective date in the past");
        }

        policy.Status = "Issued";
        policy.IssuedDate = DateTimeOffset.UtcNow;
        policy = await _repository.UpdateAsync(policy);

        // Publish PolicyIssued event (triggers billing account creation)
        await _messageSession.Publish(new PolicyIssued(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            PolicyId: policy.PolicyId,
            PolicyNumber: policy.PolicyNumber,
            CustomerId: policy.CustomerId,
            StructureCoverageLimit: policy.StructureCoverageLimit,
            StructureDeductible: policy.StructureDeductible,
            ContentsCoverageLimit: policy.ContentsCoverageLimit,
            ContentsDeductible: policy.ContentsDeductible,
            TermMonths: policy.TermMonths,
            EffectiveDate: policy.EffectiveDate,
            ExpirationDate: policy.ExpirationDate,
            Premium: policy.Premium,
            IdempotencyKey: $"policy-issued-{policy.PolicyId}"
        ));

        _logger.LogInformation(
            "Policy {PolicyNumber} issued for customer {CustomerId}",
            policy.PolicyNumber, policy.CustomerId);

        return policy;
    }

    public async Task<Models.Policy> CancelPolicyAsync(string policyId, DateTimeOffset cancellationDate, string reason)
    {
        var policy = await _repository.GetByIdAsync(policyId);
        if (policy == null)
        {
            throw new InvalidOperationException($"Policy {policyId} not found");
        }

        if (policy.Status != "Active" && policy.Status != "Issued")
        {
            throw new InvalidOperationException($"Only Active or Issued policies can be cancelled. Current status: {policy.Status}");
        }

        if (cancellationDate < DateTimeOffset.UtcNow.Date.AddDays(1))
        {
            throw new InvalidOperationException("Cancellation date must be at least 1 day in the future");
        }

        if (cancellationDate > policy.ExpirationDate)
        {
            throw new InvalidOperationException("Cancellation date cannot be after expiration date");
        }

        // Calculate unearned premium
        var totalDays = (policy.ExpirationDate - policy.EffectiveDate).TotalDays;
        var daysRemaining = (policy.ExpirationDate - cancellationDate).TotalDays;
        var unearnedPercentage = daysRemaining / totalDays;
        var unearnedPremium = Math.Round(policy.Premium * (decimal)unearnedPercentage, 2);

        policy.Status = "Cancelled";
        policy.CancelledDate = cancellationDate;
        policy.CancellationReason = reason;
        policy.UnearnedPremium = unearnedPremium;
        policy = await _repository.UpdateAsync(policy);

        // Publish PolicyCancelled event (triggers refund in Billing)
        await _messageSession.Publish(new PolicyCancelled(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            PolicyId: policy.PolicyId,
            CustomerId: policy.CustomerId,
            CancellationDate: cancellationDate,
            CancellationReason: reason,
            UnearnedPremium: unearnedPremium,
            IdempotencyKey: $"policy-cancelled-{policy.PolicyId}"
        ));

        _logger.LogInformation(
            "Policy {PolicyNumber} cancelled with unearned premium {UnearnedPremium}",
            policy.PolicyNumber, unearnedPremium);

        return policy;
    }

    public async Task<Models.Policy> ReinstatePolicyAsync(string policyId)
    {
        var policy = await _repository.GetByIdAsync(policyId);
        if (policy == null)
        {
            throw new InvalidOperationException($"Policy {policyId} not found");
        }

        if (policy.Status != "Lapsed")
        {
            throw new InvalidOperationException($"Only Lapsed policies can be reinstated. Current status: {policy.Status}");
        }

        // Verify lapsed less than 30 days ago
        // (Assuming we'd have a LapsedDate field - simplified for now)

        policy.Status = "Reinstated";
        policy = await _repository.UpdateAsync(policy);

        // Publish PolicyReinstated event
        await _messageSession.Publish(new PolicyReinstated(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            PolicyId: policy.PolicyId,
            PolicyNumber: policy.PolicyNumber,
            CustomerId: policy.CustomerId,
            PaymentAmount: policy.Premium,  // Simplified - would calculate actual amount
            IdempotencyKey: $"policy-reinstated-{policy.PolicyId}"
        ));

        _logger.LogInformation(
            "Policy {PolicyNumber} reinstated for customer {CustomerId}",
            policy.PolicyNumber, policy.CustomerId);

        return policy;
    }

    public async Task<Models.Policy> GetPolicyAsync(string policyId)
    {
        var policy = await _repository.GetByIdAsync(policyId);
        if (policy == null)
        {
            throw new InvalidOperationException($"Policy {policyId} not found");
        }

        return policy;
    }

    public async Task<IEnumerable<Models.Policy>> GetCustomerPoliciesAsync(string customerId)
    {
        return await _repository.GetByCustomerIdAsync(customerId);
    }
}
