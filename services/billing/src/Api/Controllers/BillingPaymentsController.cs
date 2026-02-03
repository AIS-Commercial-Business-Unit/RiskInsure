namespace RiskInsure.Billing.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using NServiceBus;
using RiskInsure.Billing.Api.Models;
using RiskInsure.Billing.Domain.Contracts.Commands;
using RiskInsure.Billing.Domain.Managers;
using RiskInsure.Billing.Domain.Managers.DTOs;

/// <summary>
/// API controller for billing payment operations.
/// Supports both synchronous (direct manager call) and asynchronous (command publishing) patterns.
/// </summary>
[ApiController]
[Route("api/billing/payments")]
[Produces("application/json")]
public class BillingPaymentsController : ControllerBase
{
    private readonly IBillingPaymentManager _paymentManager;
    private readonly IMessageSession _messageSession;
    private readonly ILogger<BillingPaymentsController> _logger;

    public BillingPaymentsController(
        IBillingPaymentManager paymentManager,
        IMessageSession messageSession,
        ILogger<BillingPaymentsController> logger)
    {
        _paymentManager = paymentManager ?? throw new ArgumentNullException(nameof(paymentManager));
        _messageSession = messageSession ?? throw new ArgumentNullException(nameof(messageSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Records a payment to a billing account synchronously.
    /// Returns immediate result after processing business rules.
    /// </summary>
    /// <param name="request">Payment details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>200 OK with updated account details, or error response</returns>
    /// <response code="200">Payment successfully recorded</response>
    /// <response code="400">Invalid request data or business rule violation</response>
    /// <response code="404">Billing account not found</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordPaymentSync(
        [FromBody] RecordPaymentRequest request,
        CancellationToken cancellationToken)
    {
        // Structural validation (DTO layer)
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid RecordPayment request: {ValidationErrors}",
                string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return BadRequest(ModelState);
        }

        // Map to domain DTO
        var dto = new RecordPaymentDto
        {
            AccountId = request.AccountId,
            Amount = request.Amount,
            ReferenceNumber = request.ReferenceNumber,
            IdempotencyKey = $"{request.AccountId}:{request.ReferenceNumber}",
            OccurredUtc = DateTimeOffset.UtcNow
        };

        // Call Manager (business logic)
        var result = await _paymentManager.RecordPaymentAsync(dto, cancellationToken);

        // Handle result
        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Failed to record payment for account {AccountId}: {ErrorMessage} ({ErrorCode})",
                request.AccountId, result.ErrorMessage, result.ErrorCode);

            return result.ErrorCode switch
            {
                "ACCOUNT_NOT_FOUND" => NotFound(new { Error = result.ErrorMessage, ErrorCode = result.ErrorCode }),
                _ => BadRequest(new { Error = result.ErrorMessage, ErrorCode = result.ErrorCode })
            };
        }

        var account = result.UpdatedAccount!;
        return Ok(new
        {
            Message = result.WasDuplicate ? "Duplicate payment - already processed" : "Payment successfully recorded",
            AccountId = account.AccountId,
            Amount = request.Amount,
            ReferenceNumber = request.ReferenceNumber,
            TotalPaid = account.TotalPaid,
            OutstandingBalance = account.OutstandingBalance,
            WasDuplicate = result.WasDuplicate
        });
    }

    /// <summary>
    /// Records a payment to a billing account asynchronously.
    /// Publishes command to NServiceBus for background processing.
    /// </summary>
    /// <param name="request">Payment details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>202 Accepted - Payment command queued for processing</returns>
    /// <response code="202">Payment command accepted and queued for processing</response>
    /// <response code="400">Invalid request data</response>
    [HttpPost("async")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordPaymentAsync(
        [FromBody] RecordPaymentRequest request,
        CancellationToken cancellationToken)
    {
        // Structural validation (DTO layer)
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid RecordPayment request: {ValidationErrors}",
                string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return BadRequest(ModelState);
        }

        // Create command
        var idempotencyKey = $"{request.AccountId}:{request.ReferenceNumber}";
        var command = new RecordPayment(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            AccountId: request.AccountId,
            Amount: request.Amount,
            ReferenceNumber: request.ReferenceNumber,
            IdempotencyKey: idempotencyKey
        );

        // Publish to NServiceBus
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
        return Ok(new { Status = "Healthy", Service = "Billing Payments API" });
    }
}
