using Microsoft.AspNetCore.Mvc;
using RiskInsure.FundTransferMgt.Api.Models;
using RiskInsure.FundTransferMgt.Domain.Managers;
using RiskInsure.FundTransferMgt.Domain.Models;

namespace RiskInsure.FundTransferMgt.Api.Controllers;

[ApiController]
[Route("api/payment-methods")]
public class PaymentMethodsController : ControllerBase
{
    private readonly IPaymentMethodManager _manager;
    private readonly ILogger<PaymentMethodsController> _logger;

    public PaymentMethodsController(
        IPaymentMethodManager manager,
        ILogger<PaymentMethodsController> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    [HttpPost("credit-card")]
    public async Task<ActionResult<PaymentMethodResponse>> AddCreditCard(
        [FromBody] AddCreditCardRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var address = new Address
            {
                Street = request.BillingAddress.Street,
                City = request.BillingAddress.City,
                State = request.BillingAddress.State,
                PostalCode = request.BillingAddress.PostalCode,
                Country = request.BillingAddress.Country
            };

            var paymentMethod = await _manager.AddCreditCardAsync(
                request.PaymentMethodId,
                request.CustomerId,
                request.CardholderName,
                request.CardNumber,
                request.ExpirationMonth,
                request.ExpirationYear,
                request.Cvv,
                address,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetPaymentMethod),
                new { paymentMethodId = paymentMethod.PaymentMethodId },
                MapToResponse(paymentMethod));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid credit card data");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to add credit card");
            return StatusCode(500, new { error = "Failed to process credit card" });
        }
    }

    [HttpPost("ach")]
    public async Task<ActionResult<PaymentMethodResponse>> AddAchAccount(
        [FromBody] AddAchAccountRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var paymentMethod = await _manager.AddAchAccountAsync(
                request.PaymentMethodId,
                request.CustomerId,
                request.AccountHolderName,
                request.RoutingNumber,
                request.AccountNumber,
                request.AccountType,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetPaymentMethod),
                new { paymentMethodId = paymentMethod.PaymentMethodId },
                MapToResponse(paymentMethod));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid ACH account data");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to add ACH account");
            return StatusCode(500, new { error = "Failed to process ACH account" });
        }
    }

    [HttpGet("{paymentMethodId}")]
    public async Task<ActionResult<PaymentMethodResponse>> GetPaymentMethod(
        string paymentMethodId,
        CancellationToken cancellationToken)
    {
        var paymentMethod = await _manager.GetPaymentMethodAsync(paymentMethodId, cancellationToken);
        
        if (paymentMethod == null)
            return NotFound();

        return Ok(MapToResponse(paymentMethod));
    }

    [HttpGet]
    public async Task<ActionResult<List<PaymentMethodResponse>>> GetCustomerPaymentMethods(
        [FromQuery] string customerId,
        CancellationToken cancellationToken)
    {
        var paymentMethods = await _manager.GetCustomerPaymentMethodsAsync(customerId, cancellationToken);
        return Ok(paymentMethods.Select(MapToResponse).ToList());
    }

    [HttpDelete("{paymentMethodId}")]
    public async Task<ActionResult> RemovePaymentMethod(
        string paymentMethodId,
        CancellationToken cancellationToken)
    {
        await _manager.RemovePaymentMethodAsync(paymentMethodId, cancellationToken);
        return NoContent();
    }

    private static PaymentMethodResponse MapToResponse(PaymentMethod pm) => new(
        pm.PaymentMethodId,
        pm.CustomerId,
        FormatPaymentMethodType(pm.Type),
        pm.Status.ToString(),
        pm.Card != null ? new CardDetailsDto(
            pm.Card.CardholderName,
            pm.Card.Last4,
            pm.Card.Brand,
            pm.Card.ExpirationMonth,
            pm.Card.ExpirationYear
        ) : null,
        pm.Ach != null ? new AchDetailsDto(
            pm.Ach.AccountHolderName,
            pm.Ach.RoutingNumber,
            pm.Ach.Last4,
            pm.Ach.AccountType
        ) : null,
        pm.CreatedUtc
    );

    private static string FormatPaymentMethodType(PaymentMethodType type) => type switch
    {
        PaymentMethodType.Ach => "ACH",
        _ => type.ToString()
    };
}
