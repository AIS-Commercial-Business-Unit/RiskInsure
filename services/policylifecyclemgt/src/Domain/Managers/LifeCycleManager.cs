namespace RiskInsure.PolicyLifeCycleMgt.Domain.Managers;

using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.PolicyLifeCycleMgt.Domain.Contracts.Events;
using RiskInsure.PolicyLifeCycleMgt.Domain.Repositories;
using RiskInsure.PolicyLifeCycleMgt.Domain.Services;
using RiskInsure.PublicContracts.Events;

public class LifeCycleManager : ILifeCycleManager
{
    private readonly ILifeCycleRepository _repository;
    private readonly ILifeCycleNumberGenerator _lifeCycleNumberGenerator;
    private readonly IMessageSession _messageSession;
    private readonly ILogger<LifeCycleManager> _logger;

    public LifeCycleManager(
        ILifeCycleRepository repository,
        ILifeCycleNumberGenerator lifeCycleNumberGenerator,
        IMessageSession messageSession,
        ILogger<LifeCycleManager> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _lifeCycleNumberGenerator = lifeCycleNumberGenerator ?? throw new ArgumentNullException(nameof(lifeCycleNumberGenerator));
        _messageSession = messageSession ?? throw new ArgumentNullException(nameof(messageSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Models.LifeCycle> CreateFromQuoteAsync(QuoteAccepted quote)
    {
        _logger.LogInformation(
            "CreateFromQuoteAsync called for QuoteId {QuoteId}",
            quote.QuoteId);

        // Idempotency check
        var existing = await _repository.GetByQuoteIdAsync(quote.QuoteId);
        if (existing != null)
        {
            _logger.LogInformation(
                "Lifecycle already exists for QuoteId {QuoteId}, returning existing lifecycle {PolicyNumber}",
                quote.QuoteId, existing.PolicyNumber);
            return existing;
        }

        // Generate lifecycle number and ID
        _logger.LogInformation("Generating lifecycle number...");
        var lifeCycleNumber = await _lifeCycleNumberGenerator.GenerateAsync();
        _logger.LogInformation("Lifecycle number generated: {PolicyNumber}", lifeCycleNumber);

        var policyId = Guid.NewGuid().ToString();
        _logger.LogInformation(
            "Creating policy with Id={PolicyId}, PolicyId={PolicyId}",
            policyId, policyId);

        // Create policy
        var policy = new Models.LifeCycle
        {
            Id = policyId,           // Cosmos DB id
            PolicyId = policyId,     // Partition key (must match Id)
            PolicyNumber = lifeCycleNumber,
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

        // Publish LifeCycleInitiated event
        await _messageSession.Publish(new LifeCycleInitiated(
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
            "Lifecycle {PolicyNumber} initiated for Quote {QuoteId}",
            lifeCycleNumber, quote.QuoteId);

        return policy;
    }

    public async Task<Models.LifeCycle> IssueLifeCycleAsync(string policyId)
    {
        var policy = await _repository.GetByIdAsync(policyId);
        if (policy == null)
        {
            throw new InvalidOperationException($"LifeCycle {policyId} not found");
        }

        if (policy.Status != "Bound")
        {
            throw new InvalidOperationException($"LifeCycle must be in 'Bound' status to issue. Current status: {policy.Status}");
        }

        if (policy.EffectiveDate < DateTimeOffset.UtcNow.Date)
        {
            throw new InvalidOperationException("Cannot issue lifecycle with effective date in the past");
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
            "Lifecycle {PolicyNumber} issued for customer {CustomerId}",
            policy.PolicyNumber, policy.CustomerId);

        return policy;
    }

    public async Task<Models.LifeCycle> CancelLifeCycleAsync(string policyId, DateTimeOffset cancellationDate, string reason)
    {
        var policy = await _repository.GetByIdAsync(policyId);
        if (policy == null)
        {
            throw new InvalidOperationException($"LifeCycle {policyId} not found");
        }

        if (policy.Status != "Active" && policy.Status != "Issued")
        {
            throw new InvalidOperationException($"Only Active or Issued lifecycles can be cancelled. Current status: {policy.Status}");
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

        // Publish LifeCycleCancelled event (triggers refund in Billing)
        await _messageSession.Publish(new LifeCycleCancelled(
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
            "Lifecycle {PolicyNumber} cancelled with unearned premium {UnearnedPremium}",
            policy.PolicyNumber, unearnedPremium);

        return policy;
    }

    public async Task<Models.LifeCycle> ReinstateLifeCycleAsync(string policyId)
    {
        var policy = await _repository.GetByIdAsync(policyId);
        if (policy == null)
        {
            throw new InvalidOperationException($"LifeCycle {policyId} not found");
        }

        if (policy.Status != "Lapsed")
        {
            throw new InvalidOperationException($"Only Lapsed lifecycles can be reinstated. Current status: {policy.Status}");
        }

        // Verify lapsed less than 30 days ago
        // (Assuming we'd have a LapsedDate field - simplified for now)

        policy.Status = "Reinstated";
        policy = await _repository.UpdateAsync(policy);

        // Publish LifeCycleReinstated event
        await _messageSession.Publish(new LifeCycleReinstated(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            PolicyId: policy.PolicyId,
            PolicyNumber: policy.PolicyNumber,
            CustomerId: policy.CustomerId,
            PaymentAmount: policy.Premium,  // Simplified - would calculate actual amount
            IdempotencyKey: $"policy-reinstated-{policy.PolicyId}"
        ));

        _logger.LogInformation(
            "Lifecycle {PolicyNumber} reinstated for customer {CustomerId}",
            policy.PolicyNumber, policy.CustomerId);

        return policy;
    }

    public async Task<Models.LifeCycle> GetLifeCycleAsync(string policyId)
    {
        var policy = await _repository.GetByIdAsync(policyId);
        if (policy == null)
        {
            throw new InvalidOperationException($"LifeCycle {policyId} not found");
        }

        return policy;
    }

    public async Task<IEnumerable<Models.LifeCycle>> GetCustomerLifeCyclesAsync(string customerId)
    {
        return await _repository.GetByCustomerIdAsync(customerId);
    }
}
