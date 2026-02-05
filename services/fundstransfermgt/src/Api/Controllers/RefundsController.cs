using Microsoft.AspNetCore.Mvc;
using RiskInsure.FundTransferMgt.Api.Models;
using RiskInsure.FundTransferMgt.Domain.Managers;
using RiskInsure.FundTransferMgt.Domain.Models;

namespace RiskInsure.FundTransferMgt.Api.Controllers;

[ApiController]
[Route("api/refunds")]
public class RefundsController : ControllerBase
{
    private readonly IFundTransferManager _manager;
    private readonly ILogger<RefundsController> _logger;

    public RefundsController(
        IFundTransferManager manager,
        ILogger<RefundsController> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<RefundResponse>> ProcessRefund(
        [FromBody] ProcessRefundRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var refund = await _manager.ProcessRefundAsync(
                request.OriginalTransactionId,
                request.Amount,
                request.Reason,
                cancellationToken);

            return Ok(MapToResponse(refund));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to process refund");
            return BadRequest(new { error = ex.Message });
        }
    }

    private static RefundResponse MapToResponse(Refund r) => new(
        r.RefundId,
        r.CustomerId,
        r.OriginalTransactionId,
        r.Amount,
        r.Status.ToString(),
        r.Reason,
        r.InitiatedUtc,
        r.ProcessedUtc
    );
}
