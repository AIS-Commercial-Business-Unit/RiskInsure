namespace RiskInsure.Policy.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using RiskInsure.Policy.Api.Models;
using RiskInsure.Policy.Domain.Managers;

[ApiController]
[Route("api/policies")]
[Produces("application/json")]
public class PoliciesController : ControllerBase
{
    private readonly IPolicyManager _manager;
    private readonly ILogger<PoliciesController> _logger;

    public PoliciesController(
        IPolicyManager manager,
        ILogger<PoliciesController> logger)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("{policyId}/issue")]
    [ProducesResponseType(typeof(PolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IssuePolicy(string policyId)
    {
        try
        {
            var policy = await _manager.IssuePolicyAsync(policyId);

            return Ok(new PolicyResponse
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
                TermMonths = policy.TermMonths
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = "PolicyNotFound", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = "InvalidPolicyStatus", message = ex.Message });
        }
    }

    [HttpGet("{policyId}")]
    [ProducesResponseType(typeof(PolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPolicy(string policyId)
    {
        try
        {
            var policy = await _manager.GetPolicyAsync(policyId);

            return Ok(new PolicyResponse
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
                TermMonths = policy.TermMonths
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = "PolicyNotFound", message = ex.Message });
        }
    }

    [HttpGet("/api/customers/{customerId}/policies")]
    [ProducesResponseType(typeof(CustomerPoliciesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerPolicies(string customerId)
    {
        var policies = await _manager.GetCustomerPoliciesAsync(customerId);

        return Ok(new CustomerPoliciesResponse
        {
            Policies = policies.Select(p => new PolicySummary
            {
                PolicyId = p.PolicyId,
                PolicyNumber = p.PolicyNumber,
                Status = p.Status,
                EffectiveDate = p.EffectiveDate,
                ExpirationDate = p.ExpirationDate,
                Premium = p.Premium
            }).ToList()
        });
    }

    [HttpPost("{policyId}/cancel")]
    [ProducesResponseType(typeof(CancelPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelPolicy(string policyId, [FromBody] CancelPolicyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var policy = await _manager.CancelPolicyAsync(
                policyId,
                request.CancellationDate,
                request.Reason);

            return Ok(new CancelPolicyResponse
            {
                PolicyId = policy.PolicyId,
                Status = policy.Status,
                CancellationDate = policy.CancelledDate!.Value,
                UnearnedPremium = policy.UnearnedPremium!.Value
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = "PolicyNotFound", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "ValidationFailed", errors = new { General = new[] { ex.Message } } });
        }
    }

    [HttpPost("{policyId}/reinstate")]
    [ProducesResponseType(typeof(PolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReinstatePolicy(string policyId)
    {
        try
        {
            var policy = await _manager.ReinstatePolicyAsync(policyId);

            return Ok(new PolicyResponse
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
                TermMonths = policy.TermMonths
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = "PolicyNotFound", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "ValidationFailed", errors = new { General = new[] { ex.Message } } });
        }
    }
}
