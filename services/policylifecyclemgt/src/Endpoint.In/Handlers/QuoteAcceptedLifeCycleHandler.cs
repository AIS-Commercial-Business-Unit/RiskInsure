namespace RiskInsure.PolicyLifeCycleMgt.Endpoint.In.Handlers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NServiceBus;
using RiskInsure.PolicyLifeCycleMgt.Domain.Managers;
using RiskInsure.PolicyLifeCycleMgt.Endpoint.In.Configuration;
using RiskInsure.PublicContracts.Events;
using System.Security.Cryptography;
using System.Text;

public class QuoteAcceptedLifeCycleHandler : IHandleMessages<QuoteAccepted>
{
    private readonly ILifeCycleManager _manager;
    private readonly ILogger<QuoteAcceptedLifeCycleHandler> _logger;
    private readonly LifeCycleTrafficOptions _trafficOptions;

    public QuoteAcceptedLifeCycleHandler(
        ILifeCycleManager manager,
        ILogger<QuoteAcceptedLifeCycleHandler> logger,
        IOptions<LifeCycleTrafficOptions> trafficOptions)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _trafficOptions = trafficOptions?.Value ?? new LifeCycleTrafficOptions();
    }

    public async Task Handle(QuoteAccepted message, IMessageHandlerContext context)
    {
        if (!ShouldProcessQuote(message.QuoteId))
        {
            _logger.LogInformation(
                "Skipping QuoteAccepted for QuoteId {QuoteId} due to lifecycle cutover gate. Enabled={Enabled}, Percentage={Percentage}",
                message.QuoteId,
                _trafficOptions.EnableLifeCycleProcessing,
                _trafficOptions.LifeCycleProcessingPercentage);
            return;
        }

        _logger.LogInformation(
            "Handling QuoteAccepted event for QuoteId {QuoteId}, CustomerId {CustomerId}",
            message.QuoteId, message.CustomerId);

        try
        {
            var policy = await _manager.CreateFromQuoteAsync(message);

            _logger.LogInformation(
                "Policy {PolicyNumber} created for Quote {QuoteId}",
                policy.PolicyNumber, message.QuoteId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create policy for Quote {QuoteId}: {ErrorMessage}",
                message.QuoteId, ex.Message);
            throw;
        }
    }

    private bool ShouldProcessQuote(string quoteId)
    {
        if (!_trafficOptions.EnableLifeCycleProcessing)
        {
            return false;
        }

        var percentage = Math.Clamp(_trafficOptions.LifeCycleProcessingPercentage, 0, 100);
        if (percentage >= 100)
        {
            return true;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(quoteId));
        var bucket = hash[0] % 100;
        return bucket < percentage;
    }
}
