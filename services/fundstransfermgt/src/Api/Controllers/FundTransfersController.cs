using Microsoft.AspNetCore.Mvc;
using RiskInsure.FundTransferMgt.Api.Models;
using RiskInsure.FundTransferMgt.Domain.Managers;
using RiskInsure.FundTransferMgt.Domain.Models;

namespace RiskInsure.FundTransferMgt.Api.Controllers;

[ApiController]
[Route("api/fund-transfers")]
public class FundTransfersController : ControllerBase
{
    private readonly IFundTransferManager _manager;
    private readonly ILogger<FundTransfersController> _logger;

    public FundTransfersController(
        IFundTransferManager manager,
        ILogger<FundTransfersController> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<TransferResponse>> InitiateTransfer(
        [FromBody] InitiateTransferRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var transfer = await _manager.InitiateTransferAsync(
                request.CustomerId,
                request.PaymentMethodId,
                request.Amount,
                request.Purpose,
                cancellationToken);

            return Ok(MapToResponse(transfer));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to initiate transfer");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TransferResponse>> GetTransfer(
        string id,
        CancellationToken cancellationToken)
    {
        var transfer = await _manager.GetTransferAsync(id, cancellationToken);
        
        if (transfer == null)
            return NotFound();

        return Ok(MapToResponse(transfer));
    }

    [HttpGet]
    public async Task<ActionResult<List<TransferResponse>>> GetCustomerTransfers(
        [FromQuery] string customerId,
        CancellationToken cancellationToken)
    {
        var transfers = await _manager.GetCustomerTransfersAsync(customerId, cancellationToken);
        return Ok(transfers.Select(MapToResponse).ToList());
    }

    private static TransferResponse MapToResponse(FundTransfer t) => new(
        t.TransactionId,
        t.CustomerId,
        t.PaymentMethodId,
        t.Amount,
        t.Direction.ToString(),
        t.Status.ToString(),
        t.Purpose,
        t.InitiatedUtc,
        t.SettledUtc
    );
}
