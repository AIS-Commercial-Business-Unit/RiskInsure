namespace RiskInsure.PolicyLifeCycleMgt.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using RiskInsure.PolicyLifeCycleMgt.Api.Models;
using RiskInsure.PolicyLifeCycleMgt.Domain.Managers;

[ApiController]
[Route("api/lifecycles")]
[Produces("application/json")]
public class LifeCyclesController : ControllerBase
{
    private readonly ILifeCycleManager _manager;
    private readonly ILogger<LifeCyclesController> _logger;

    public LifeCyclesController(
        ILifeCycleManager manager,
        ILogger<LifeCyclesController> logger)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("{policyId}/issue")]
    [ProducesResponseType(typeof(LifeCycleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IssueLifeCycle(string policyId)
    {
        try
        {
            var policy = await _manager.IssueLifeCycleAsync(policyId);

            return Ok(new LifeCycleResponse
            {
                PolicyId = policy.PolicyId,
                PolicyNumber = policy.PolicyNumber,
                CustomerId = policy.CustomerId,
                Status = policy.Status,
                EffectiveDate = policy.EffectiveDate,
                ExpirationDate = policy.ExpirationDate,
                Premium = policy.Premium,
                BoundDate = policy.BoundDate,
                IssuedDate = policy.IssuedDate,
                StructureCoverageLimit = policy.StructureCoverageLimit,
                StructureDeductible = policy.StructureDeductible,
                ContentsCoverageLimit = policy.ContentsCoverageLimit,
                ContentsDeductible = policy.ContentsDeductible,
                TermMonths = policy.TermMonths,
                CreatedUtc = policy.CreatedUtc
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = "LifeCycleNotFound", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = "InvalidLifeCycleStatus", message = ex.Message });
        }
    }

    [HttpGet("{policyId}")]
    [ProducesResponseType(typeof(LifeCycleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLifeCycle(string policyId)
    {
        try
        {
            var policy = await _manager.GetLifeCycleAsync(policyId);

            return Ok(new LifeCycleResponse
            {
                PolicyId = policy.PolicyId,
                PolicyNumber = policy.PolicyNumber,
                CustomerId = policy.CustomerId,
                Status = policy.Status,
                EffectiveDate = policy.EffectiveDate,
                ExpirationDate = policy.ExpirationDate,
                Premium = policy.Premium,
                BoundDate = policy.BoundDate,
                IssuedDate = policy.IssuedDate,
                CancelledDate = policy.CancelledDate,
                CancellationReason = policy.CancellationReason,
                UnearnedPremium = policy.UnearnedPremium,
                StructureCoverageLimit = policy.StructureCoverageLimit,
                StructureDeductible = policy.StructureDeductible,
                ContentsCoverageLimit = policy.ContentsCoverageLimit,
                ContentsDeductible = policy.ContentsDeductible,
                TermMonths = policy.TermMonths,
                CreatedUtc = policy.CreatedUtc
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = "LifeCycleNotFound", message = ex.Message });
        }
    }

    [HttpGet("/api/customers/{customerId}/lifecycles")]
    [ProducesResponseType(typeof(CustomerLifeCyclesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerLifeCycles(string customerId)
    {
        var lifeCycles = await _manager.GetCustomerLifeCyclesAsync(customerId);

        return Ok(new CustomerLifeCyclesResponse
        {CustomerId = customerId,
            
            LifeCycles = lifeCycles.Select(p => new LifeCycleSummary
            {
                PolicyId = p.PolicyId,
                PolicyNumber = p.PolicyNumber,
                CustomerId = p.CustomerId,
                Status = p.Status,
                EffectiveDate = p.EffectiveDate,
                ExpirationDate = p.ExpirationDate,
                Premium = p.Premium,
                StructureCoverageLimit = p.StructureCoverageLimit,
                StructureDeductible = p.StructureDeductible,
                ContentsCoverageLimit = p.ContentsCoverageLimit,
                ContentsDeductible = p.ContentsDeductible,
                TermMonths = p.TermMonths,
                CreatedUtc = p.CreatedUtc
            }).ToList()
        });
    }

    [HttpPost("{policyId}/cancel")]
    [ProducesResponseType(typeof(CancelLifeCycleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelLifeCycle(string policyId, [FromBody] CancelLifeCycleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var policy = await _manager.CancelLifeCycleAsync(
                policyId,
                request.CancellationDate,
                request.Reason);

            return Ok(new CancelLifeCycleResponse
            {
                PolicyId = policy.PolicyId,
                Status = policy.Status,
                CancellationDate = policy.CancelledDate!.Value,
                UnearnedPremium = policy.UnearnedPremium!.Value
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = "LifeCycleNotFound", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "ValidationFailed", errors = new { General = new[] { ex.Message } } });
        }
    }

    [HttpPost("{policyId}/reinstate")]
    [ProducesResponseType(typeof(LifeCycleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReinstateLifeCycle(string policyId)
    {
        try
        {
            var policy = await _manager.ReinstateLifeCycleAsync(policyId);

            return Ok(new LifeCycleResponse
            {
                PolicyId = policy.PolicyId,
                PolicyNumber = policy.PolicyNumber,
                CustomerId = policy.CustomerId,
                Status = policy.Status,
                EffectiveDate = policy.EffectiveDate,
                ExpirationDate = policy.ExpirationDate,
                Premium = policy.Premium,
                BoundDate = policy.BoundDate,
                IssuedDate = policy.IssuedDate,
                StructureCoverageLimit = policy.StructureCoverageLimit,
                StructureDeductible = policy.StructureDeductible,
                ContentsCoverageLimit = policy.ContentsCoverageLimit,
                ContentsDeductible = policy.ContentsDeductible,
                TermMonths = policy.TermMonths,
                CreatedUtc = policy.CreatedUtc
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = "LifeCycleNotFound", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "ValidationFailed", errors = new { General = new[] { ex.Message } } });
        }
    }
}
