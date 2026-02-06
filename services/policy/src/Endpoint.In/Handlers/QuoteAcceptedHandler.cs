namespace RiskInsure.Policy.Endpoint.In.Handlers;

using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.Policy.Domain.Managers;
using RiskInsure.PublicContracts.Events;

public class QuoteAcceptedHandler : IHandleMessages<QuoteAccepted>
{
    private readonly IPolicyManager _manager;
    private readonly ILogger<QuoteAcceptedHandler> _logger;

    public QuoteAcceptedHandler(
        IPolicyManager manager,
        ILogger<QuoteAcceptedHandler> logger)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(QuoteAccepted message, IMessageHandlerContext context)
    {
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
}
