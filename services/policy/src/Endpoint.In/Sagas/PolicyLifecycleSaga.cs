namespace RiskInsure.Policy.Endpoint.In.Sagas;

using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.Policy.Domain.Contracts.Commands;
using RiskInsure.Policy.Domain.Contracts.Events;
using RiskInsure.Policy.Domain.Managers;

public class PolicyLifecycleSaga : Saga<PolicyLifecycleSagaData>,
    IAmStartedByMessages<StartPolicyLifecycle>,
    IHandleMessages<ApplyPolicyLifecycleMilestone>,
    IHandleMessages<ProcessPolicyEquityUpdate>,
    IHandleMessages<PolicyRenewalAccepted>
{
    private readonly IPolicyLifecycleManager _policyLifecycleManager;
    private readonly ILogger<PolicyLifecycleSaga> _logger;

    public PolicyLifecycleSaga(
        IPolicyLifecycleManager policyLifecycleManager,
        ILogger<PolicyLifecycleSaga> logger)
    {
        _policyLifecycleManager = policyLifecycleManager ?? throw new ArgumentNullException(nameof(policyLifecycleManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private void EnsureSagaDataInitialized()
    {
        Data ??= new PolicyLifecycleSagaData();
    }

    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<PolicyLifecycleSagaData> mapper)
    {
        mapper.MapSaga(data => data.PolicyTermId)
            .ToMessage<StartPolicyLifecycle>(message => message.PolicyTermId)
            .ToMessage<ApplyPolicyLifecycleMilestone>(message => message.PolicyTermId)
            .ToMessage<ProcessPolicyEquityUpdate>(message => message.PolicyTermId)
            .ToMessage<PolicyRenewalAccepted>(message => message.CurrentPolicyTermId);
    }

    public async Task Handle(StartPolicyLifecycle message, IMessageHandlerContext context)
    {
        EnsureSagaDataInitialized();
        var state = await _policyLifecycleManager.StartLifecycleAsync(message);

        Data.PolicyId = state.PolicyId;
        Data.PolicyTermId = state.PolicyTermId;
        Data.CurrentStatus = state.CurrentStatus;

        _logger.LogInformation(
            "Started PolicyLifecycleSaga for PolicyId {PolicyId}, PolicyTermId {PolicyTermId}",
            Data.PolicyId,
            Data.PolicyTermId);
    }

    public async Task Handle(ApplyPolicyLifecycleMilestone message, IMessageHandlerContext context)
    {
        EnsureSagaDataInitialized();
        var state = await _policyLifecycleManager.ApplyMilestoneAsync(message);
        if (state is null)
        {
            _logger.LogWarning(
                "Ignoring lifecycle milestone for missing PolicyTermId {PolicyTermId}",
                message.PolicyTermId);
            return;
        }

        Data.CurrentStatus = state.CurrentStatus;
    }

    public async Task Handle(ProcessPolicyEquityUpdate message, IMessageHandlerContext context)
    {
        EnsureSagaDataInitialized();
        var state = await _policyLifecycleManager.ProcessEquityUpdateAsync(message);
        if (state is null)
        {
            _logger.LogWarning(
                "Ignoring equity update for missing PolicyTermId {PolicyTermId}",
                message.PolicyTermId);
            return;
        }

        Data.CurrentStatus = state.CurrentStatus;

        var cancellationRecommended = string.Equals(state.CurrentStatus, "PendingCancellation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state.CurrentStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);

        await context.Publish(new PolicyEquityLevelReported(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            PolicyId: state.PolicyId,
            PolicyTermId: state.PolicyTermId,
            EquityPercentage: state.CurrentEquityPercentage ?? message.EquityPercentage,
            CancellationThresholdPercentage: state.CancellationThresholdPercentage,
            CancellationRecommended: cancellationRecommended,
            IdempotencyKey: message.IdempotencyKey));
    }

    public async Task Handle(PolicyRenewalAccepted message, IMessageHandlerContext context)
    {
        EnsureSagaDataInitialized();
        var state = await _policyLifecycleManager.HandleRenewalAcceptedAsync(message);
        if (state is null)
        {
            _logger.LogWarning(
                "Ignoring renewal accepted for missing CurrentPolicyTermId {PolicyTermId}",
                message.CurrentPolicyTermId);
            return;
        }

        Data.CurrentStatus = state.CurrentStatus;
        Data.CompletionStatus = state.CompletionStatus;
        Data.CompletedUtc = state.CompletedUtc;

        await context.Publish(new PolicyTermCompleted(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            PolicyId: state.PolicyId,
            PolicyTermId: state.PolicyTermId,
            CompletionStatus: state.CompletionStatus ?? "Renewed",
            CompletedUtc: state.CompletedUtc ?? DateTimeOffset.UtcNow,
            IdempotencyKey: message.IdempotencyKey));

        await context.SendLocal(new StartPolicyLifecycle(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            IdempotencyKey: $"start-lifecycle-{message.PolicyId}-{message.NextPolicyTermId}",
            PolicyId: message.PolicyId,
            PolicyTermId: message.NextPolicyTermId,
            TermTicks: Math.Max(state.TermTicks, 1),
            EffectiveDate: message.NextTermEffectiveDate,
            ExpirationDate: message.NextTermExpirationDate,
            RenewalOpenPercent: state.RenewalOpenPercent,
            RenewalReminderPercent: state.RenewalReminderPercent,
            TermEndPercent: state.TermEndPercent,
            CancellationThresholdPercentage: state.CancellationThresholdPercentage,
            GraceWindowPercent: state.GraceWindowPercent));

        MarkAsComplete();
    }
}
