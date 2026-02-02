namespace RiskInsure.Billing.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using NServiceBus;
using RiskInsure.Billing.Api.Models;
using RiskInsure.Billing.Domain.Contracts.Commands;

/// <summary>
/// API controller for billing operations
/// </summary>
[ApiController]
[Route("api/billing")]
[Produces("application/json")]
public class BillingController : ControllerBase
{
    private readonly IMessageSession _messageSession;
    private readonly ILogger<BillingController> _logger;

    public BillingController(
        IMessageSession messageSession,
        ILogger<BillingController> logger)
    {
        _messageSession = messageSession ?? throw new ArgumentNullException(nameof(messageSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Records a payment to a billing account
    /// </summary>
    /// <param name="request">Payment details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>202 Accepted - Payment command queued for processing</returns>
    /// <response code="202">Payment command accepted and queued for processing</response>
    /// <response code="400">Invalid request data</response>
    [HttpPost("payments")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordPayment(
        [FromBody] RecordPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid RecordPayment request: {ValidationErrors}",
                string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return BadRequest(ModelState);
        }

        // Create idempotency key from reference number and account
        var idempotencyKey = $"{request.AccountId}:{request.ReferenceNumber}";

        var command = new RecordPayment(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            AccountId: request.AccountId,
            Amount: request.Amount,
            ReferenceNumber: request.ReferenceNumber,
            IdempotencyKey: idempotencyKey
        );

        await _messageSession.Send(command, cancellationToken);

        _logger.LogInformation(
            "RecordPayment command sent for account {AccountId}, Amount={Amount}, Reference={ReferenceNumber}",
            request.AccountId, request.Amount, request.ReferenceNumber);

        return Accepted(new
        {
            Message = "Payment command accepted and queued for processing",
            AccountId = request.AccountId,
            Amount = request.Amount,
            ReferenceNumber = request.ReferenceNumber
        });
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    /// <returns>200 OK if service is healthy</returns>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Service = "Billing API" });
    }
}
