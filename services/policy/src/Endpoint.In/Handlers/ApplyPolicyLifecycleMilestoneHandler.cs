namespace RiskInsure.Policy.Endpoint.In.Handlers;

using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.Policy.Domain.Contracts.Commands;
using RiskInsure.Policy.Domain.Managers;

public class ApplyPolicyLifecycleMilestoneHandler : IHandleMessages<ApplyPolicyLifecycleMilestone>
{
    private readonly IPolicyLifecycleManager _lifecycleManager;
    private readonly ILogger<ApplyPolicyLifecycleMilestoneHandler> _logger;

    public ApplyPolicyLifecycleMilestoneHandler(
        IPolicyLifecycleManager lifecycleManager,
        ILogger<ApplyPolicyLifecycleMilestoneHandler> logger)
    {
        _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ApplyPolicyLifecycleMilestone message, IMessageHandlerContext context)
    {
        var state = await _lifecycleManager.ApplyMilestoneAsync(message);

        if (state is null)
        {
            _logger.LogWarning(
                "Lifecycle milestone ignored for missing PolicyTermId {PolicyTermId}",
                message.PolicyTermId);
            return;
        }

        _logger.LogInformation(
            "Lifecycle milestone {MilestoneType} applied for PolicyTermId {PolicyTermId}, CurrentStatus {CurrentStatus}",
            message.MilestoneType,
            message.PolicyTermId,
            state.CurrentStatus);
    }
}
