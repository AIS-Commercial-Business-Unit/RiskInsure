namespace RiskInsure.Policy.Endpoint.In.Handlers;

using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.Policy.Domain.Contracts.Commands;
using RiskInsure.Policy.Domain.Managers;

public class ProcessPolicyEquityUpdateHandler : IHandleMessages<ProcessPolicyEquityUpdate>
{
    private readonly IPolicyLifecycleManager _lifecycleManager;
    private readonly ILogger<ProcessPolicyEquityUpdateHandler> _logger;

    public ProcessPolicyEquityUpdateHandler(
        IPolicyLifecycleManager lifecycleManager,
        ILogger<ProcessPolicyEquityUpdateHandler> logger)
    {
        _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ProcessPolicyEquityUpdate message, IMessageHandlerContext context)
    {
        var state = await _lifecycleManager.ProcessEquityUpdateAsync(message);

        if (state is null)
        {
            _logger.LogWarning(
                "Lifecycle equity update ignored for missing PolicyTermId {PolicyTermId}",
                message.PolicyTermId);
            return;
        }

        _logger.LogInformation(
            "Lifecycle equity update processed for PolicyTermId {PolicyTermId}, CurrentStatus {CurrentStatus}, Equity {EquityPercentage}",
            message.PolicyTermId,
            state.CurrentStatus,
            message.EquityPercentage);
    }
}
