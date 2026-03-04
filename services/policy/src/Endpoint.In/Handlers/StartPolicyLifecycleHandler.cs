namespace RiskInsure.Policy.Endpoint.In.Handlers;

using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.Policy.Domain.Contracts.Commands;
using RiskInsure.Policy.Domain.Managers;

public class StartPolicyLifecycleHandler : IHandleMessages<StartPolicyLifecycle>
{
    private readonly IPolicyLifecycleManager _lifecycleManager;
    private readonly ILogger<StartPolicyLifecycleHandler> _logger;

    public StartPolicyLifecycleHandler(
        IPolicyLifecycleManager lifecycleManager,
        ILogger<StartPolicyLifecycleHandler> logger)
    {
        _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(StartPolicyLifecycle message, IMessageHandlerContext context)
    {
        await _lifecycleManager.StartLifecycleAsync(message);

        _logger.LogInformation(
            "Lifecycle start handled for PolicyId {PolicyId}, PolicyTermId {PolicyTermId}",
            message.PolicyId,
            message.PolicyTermId);
    }
}
