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
    private readonly IPolicyLifecycleManager _lifecycleManager;
    private readonly ILogger<PoliciesController> _logger;

    public PoliciesController(
        IPolicyManager manager,
        IPolicyLifecycleManager lifecycleManager,
        ILogger<PoliciesController> logger)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("{policyId}/lifecycle/start")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartLifecycle(string policyId, [FromBody] StartPolicyLifecycleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (request.TermTicks < 1)
        {
            return BadRequest(new { error = "InvalidRequest", message = "termTicks must be greater than 0" });
        }

        var command = new Domain.Contracts.Commands.StartPolicyLifecycle(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            IdempotencyKey: $"start-lifecycle-{policyId}-{request.PolicyTermId}",
            PolicyId: policyId,
            PolicyTermId: request.PolicyTermId,
            TermTicks: request.TermTicks,
            EffectiveDate: request.EffectiveDateUtc,
            ExpirationDate: request.ExpirationDateUtc,
            RenewalOpenPercent: request.RenewalOpenPercent,
            RenewalReminderPercent: request.RenewalReminderPercent,
            TermEndPercent: request.TermEndPercent,
            CancellationThresholdPercentage: request.CancellationThresholdPercentage,
            GraceWindowPercent: request.GraceWindowPercent);

        await _lifecycleManager.StartLifecycleAsync(command);

        return Accepted();
    }

    [HttpGet("{policyId}/lifecycle/terms/{policyTermId}")]
    [ProducesResponseType(typeof(PolicyTermLifecycleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPolicyTermLifecycle(string policyId, string policyTermId)
    {
        var state = await _lifecycleManager.GetLifecycleStateAsync(policyTermId);
        if (state is null || !string.Equals(state.PolicyId, policyId, StringComparison.Ordinal))
        {
            return NotFound(new { error = "PolicyTermLifecycleNotFound", message = $"Lifecycle not found for policyTermId {policyTermId}" });
        }

        return Ok(MapLifecycleState(state));
    }

    [HttpGet("{policyId}/lifecycle/terms")]
    [ProducesResponseType(typeof(List<PolicyTermLifecycleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPolicyLifecycleTerms(string policyId)
    {
        var states = await _lifecycleManager.GetLifecycleStatesByPolicyIdAsync(policyId);
        var response = states.Select(MapLifecycleState).ToList();
        return Ok(response);
    }

    [HttpPost("{policyId}/lifecycle/equity-update")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitPolicyEquitySignal(string policyId, [FromBody] PolicyEquitySignalRequest request)
    {
        var command = new Domain.Contracts.Commands.ProcessPolicyEquityUpdate(
            MessageId: Guid.NewGuid(),
            OccurredUtc: request.OccurredUtc ?? DateTimeOffset.UtcNow,
            IdempotencyKey: $"equity-update-{policyId}-{request.PolicyTermId}",
            PolicyId: policyId,
            PolicyTermId: request.PolicyTermId,
            EquityPercentage: request.EquityPercentage,
            CancellationThresholdPercentage: request.CancellationThresholdPercentage);

        var state = await _lifecycleManager.ProcessEquityUpdateAsync(command);
        if (state is null)
        {
            return NotFound(new { error = "PolicyTermLifecycleNotFound", message = $"Lifecycle not found for policyTermId {request.PolicyTermId}" });
        }

        return Accepted();
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
                TermMonths = policy.TermMonths,
                CreatedUtc = policy.CreatedUtc
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
                TermMonths = policy.TermMonths,
                CreatedUtc = policy.CreatedUtc
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
        {CustomerId = customerId,
            
            Policies = policies.Select(p => new PolicySummary
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
                TermMonths = policy.TermMonths,
                CreatedUtc = policy.CreatedUtc
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

    private static PolicyTermLifecycleResponse MapLifecycleState(Domain.Models.PolicyLifecycleTermState state)
    {
        return new PolicyTermLifecycleResponse
        {
            PolicyId = state.PolicyId,
            PolicyTermId = state.PolicyTermId,
            CurrentStatus = state.CurrentStatus,
            StatusFlags = state.StatusFlags,
            CurrentEquityPercentage = state.CurrentEquityPercentage,
            CancellationThresholdPercentage = state.CancellationThresholdPercentage,
            PendingCancellationStartedUtc = state.PendingCancellationStartedUtc,
            GraceWindowRecheckUtc = state.GraceWindowRecheckUtc,
            EffectiveDateUtc = state.EffectiveDateUtc,
            ExpirationDateUtc = state.ExpirationDateUtc,
            CompletionStatus = state.CompletionStatus,
            CompletedUtc = state.CompletedUtc
        };
    }
}
