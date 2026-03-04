namespace RiskInsure.Policy.Domain.Managers;

using Microsoft.Extensions.Logging;
using RiskInsure.Policy.Domain.Contracts.Commands;
using RiskInsure.Policy.Domain.Contracts.Events;
using RiskInsure.Policy.Domain.Models;
using RiskInsure.Policy.Domain.Repositories;

public class PolicyLifecycleManager : IPolicyLifecycleManager
{
    private readonly IPolicyLifecycleTermStateRepository _repository;
    private readonly ILogger<PolicyLifecycleManager> _logger;

    public PolicyLifecycleManager(
        IPolicyLifecycleTermStateRepository repository,
        ILogger<PolicyLifecycleManager> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<PolicyLifecycleTermState?> GetLifecycleStateAsync(string policyTermId)
    {
        return _repository.GetByPolicyTermIdAsync(policyTermId);
    }

    public Task<IEnumerable<PolicyLifecycleTermState>> GetLifecycleStatesByPolicyIdAsync(string policyId)
    {
        return _repository.GetByPolicyIdAsync(policyId);
    }

    public async Task<PolicyLifecycleTermState> StartLifecycleAsync(StartPolicyLifecycle command)
    {
        var existing = await _repository.GetByPolicyTermIdAsync(command.PolicyTermId);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Lifecycle state already exists for PolicyTermId {PolicyTermId}. Returning existing state.",
                command.PolicyTermId);
            return existing;
        }

        var state = new PolicyLifecycleTermState
        {
            Id = command.PolicyTermId,
            PolicyId = command.PolicyId,
            PolicyTermId = command.PolicyTermId,
            CurrentStatus = "Active",
            EffectiveDateUtc = command.EffectiveDate,
            ExpirationDateUtc = command.ExpirationDate,
            TermTicks = command.TermTicks,
            RenewalOpenPercent = command.RenewalOpenPercent,
            RenewalReminderPercent = command.RenewalReminderPercent,
            TermEndPercent = command.TermEndPercent,
            CancellationThresholdPercentage = command.CancellationThresholdPercentage,
            GraceWindowPercent = command.GraceWindowPercent,
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };

        return await _repository.CreateAsync(state);
    }

    public async Task<PolicyLifecycleTermState?> ApplyMilestoneAsync(ApplyPolicyLifecycleMilestone command)
    {
        var state = await _repository.GetByPolicyTermIdAsync(command.PolicyTermId);
        if (state is null)
        {
            _logger.LogWarning(
                "Lifecycle state not found for PolicyTermId {PolicyTermId} when applying milestone {MilestoneType}.",
                command.PolicyTermId,
                command.MilestoneType);
            return null;
        }

        var milestoneType = command.MilestoneType.Trim();
        if (milestoneType.Equals("RenewalOpen", StringComparison.OrdinalIgnoreCase))
        {
            if (!state.StatusFlags.Contains("PendingRenewal", StringComparer.OrdinalIgnoreCase))
            {
                state.StatusFlags.Add("PendingRenewal");
            }

            if (!string.Equals(state.CurrentStatus, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(state.CurrentStatus, "Expired", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(state.CurrentStatus, "Renewed", StringComparison.OrdinalIgnoreCase))
            {
                state.CurrentStatus = "Active";
            }
        }
        else if (milestoneType.Equals("TermEnd", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(state.CompletionStatus))
            {
                state.CurrentStatus = "Expired";
                state.CompletionStatus = "Expired";
                state.CompletedUtc = DateTimeOffset.UtcNow;
            }
        }

        state.UpdatedUtc = DateTimeOffset.UtcNow;
        return await _repository.UpdateAsync(state);
    }

    public async Task<PolicyLifecycleTermState?> ProcessEquityUpdateAsync(ProcessPolicyEquityUpdate command)
    {
        var state = await _repository.GetByPolicyTermIdAsync(command.PolicyTermId);
        if (state is null)
        {
            _logger.LogWarning(
                "Lifecycle state not found for PolicyTermId {PolicyTermId} when processing equity update.",
                command.PolicyTermId);
            return null;
        }

        state.CurrentEquityPercentage = command.EquityPercentage;
        state.CancellationThresholdPercentage = command.CancellationThresholdPercentage;

        var isBelowThreshold = command.EquityPercentage <= command.CancellationThresholdPercentage;
        if (isBelowThreshold)
        {
            if (!string.Equals(state.CurrentStatus, "PendingCancellation", StringComparison.OrdinalIgnoreCase))
            {
                state.CurrentStatus = "PendingCancellation";
                state.PendingCancellationStartedUtc = DateTimeOffset.UtcNow;

                var termDuration = state.ExpirationDateUtc - state.EffectiveDateUtc;
                var graceWindowDuration = TimeSpan.FromSeconds(
                    termDuration.TotalSeconds * (double)(state.GraceWindowPercent / 100m));

                state.GraceWindowRecheckUtc = DateTimeOffset.UtcNow.Add(graceWindowDuration);
            }
            else if (state.GraceWindowRecheckUtc.HasValue && DateTimeOffset.UtcNow >= state.GraceWindowRecheckUtc.Value)
            {
                state.CurrentStatus = "Cancelled";
                state.CompletionStatus = "Cancelled";
                state.CompletedUtc = DateTimeOffset.UtcNow;
            }
        }
        else if (string.Equals(state.CurrentStatus, "PendingCancellation", StringComparison.OrdinalIgnoreCase))
        {
            state.CurrentStatus = "Active";
            state.PendingCancellationStartedUtc = null;
            state.GraceWindowRecheckUtc = null;
        }

        state.UpdatedUtc = DateTimeOffset.UtcNow;

        return await _repository.UpdateAsync(state);
    }

    public async Task<PolicyLifecycleTermState?> HandleRenewalAcceptedAsync(PolicyRenewalAccepted policyRenewalAccepted)
    {
        var state = await _repository.GetByPolicyTermIdAsync(policyRenewalAccepted.CurrentPolicyTermId);
        if (state is null)
        {
            _logger.LogWarning(
                "Lifecycle state not found for CurrentPolicyTermId {PolicyTermId} when handling renewal accepted.",
                policyRenewalAccepted.CurrentPolicyTermId);
            return null;
        }

        state.CompletionStatus = "Renewed";
        state.CompletedUtc = DateTimeOffset.UtcNow;
        state.CurrentStatus = "Renewed";
        state.StatusFlags.RemoveAll(flag => string.Equals(flag, "PendingRenewal", StringComparison.OrdinalIgnoreCase));
        state.UpdatedUtc = DateTimeOffset.UtcNow;

        return await _repository.UpdateAsync(state);
    }
}
