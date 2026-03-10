namespace RiskInsure.PolicyEquityAndInvoicingMgt.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using RiskInsure.PolicyEquityAndInvoicingMgt.Api.Models;
using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Managers;
using RiskInsure.PolicyEquityAndInvoicingMgt.Domain.Managers.DTOs;

/// <summary>
/// API controller for policy equity and invoicing account lifecycle operations.
/// Handles account creation, activation, suspension, closure, and premium/invoicing cycle updates.
/// </summary>
[ApiController]
[Route("api/policyequityandinvoicingmgt/accounts")]
[Produces("application/json")]
public class PolicyEquityAndInvoicingAccountsController : ControllerBase
{
    private readonly IPolicyEquityAndInvoicingAccountManager _accountManager;
    private readonly ILogger<PolicyEquityAndInvoicingAccountsController> _logger;

    public PolicyEquityAndInvoicingAccountsController(
        IPolicyEquityAndInvoicingAccountManager accountManager,
        ILogger<PolicyEquityAndInvoicingAccountsController> logger)
    {
        _accountManager = accountManager ?? throw new ArgumentNullException(nameof(accountManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new policy equity and invoicing account for an insurance policy
    /// </summary>
    /// <param name="request">Account creation details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>201 Created with account details, or error response</returns>
    /// <response code="201">Account successfully created</response>
    /// <response code="400">Invalid request data or business rule violation</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAccount(
        [FromBody] CreatePolicyEquityAndInvoicingAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var dto = new CreatePolicyEquityAndInvoicingAccountDto
        {
            AccountId = request.AccountId,
            CustomerId = request.CustomerId,
            PolicyNumber = request.PolicyNumber,
            PolicyHolderName = request.PolicyHolderName,
            CurrentPremiumOwed = request.CurrentPremiumOwed,
            PolicyEquityAndInvoicingCycle = request.PolicyEquityAndInvoicingCycle,
            EffectiveDate = request.EffectiveDate
        };

        var result = await _accountManager.CreatePolicyEquityAndInvoicingAccountAsync(dto, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { Error = result.ErrorMessage, ErrorCode = result.ErrorCode });
        }

        return CreatedAtAction(
            nameof(CreateAccount),
            new { accountId = result.AccountId },
            new
            {
                Message = "Account successfully created",
                AccountId = result.AccountId,
                PolicyNumber = request.PolicyNumber,
                CurrentPremiumOwed = request.CurrentPremiumOwed
            });
    }

    /// <summary>
    /// Updates the premium owed on an existing account
    /// </summary>
    /// <param name="accountId">Account identifier</param>
    /// <param name="request">Premium update details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>200 OK or error response</returns>
    [HttpPut("{accountId}/premium")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePremiumOwed(
        string accountId,
        [FromBody] UpdatePremiumOwedRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var dto = new UpdatePremiumOwedDto
        {
            AccountId = accountId,
            NewPremiumOwed = request.NewPremiumOwed,
            ChangeReason = request.ChangeReason
        };

        var result = await _accountManager.UpdatePremiumOwedAsync(dto, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ACCOUNT_NOT_FOUND" => NotFound(new { Error = result.ErrorMessage, ErrorCode = result.ErrorCode }),
                _ => BadRequest(new { Error = result.ErrorMessage, ErrorCode = result.ErrorCode })
            };
        }

        return Ok(new
        {
            Message = "Premium updated successfully",
            AccountId = result.AccountId,
            NewPremiumOwed = request.NewPremiumOwed
        });
    }

    /// <summary>
    /// Activates a pending policy equity and invoicing account
    /// </summary>
    /// <param name="accountId">Account identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>200 OK or error response</returns>
    [HttpPost("{accountId}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateAccount(
        string accountId,
        CancellationToken cancellationToken)
    {
        var result = await _accountManager.ActivateAccountAsync(accountId, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ACCOUNT_NOT_FOUND" => NotFound(new { Error = result.ErrorMessage, ErrorCode = result.ErrorCode }),
                _ => BadRequest(new { Error = result.ErrorMessage, ErrorCode = result.ErrorCode })
            };
        }

        return Ok(new
        {
            Message = "Account activated successfully",
            AccountId = result.AccountId
        });
    }

    /// <summary>
    /// Suspends an active policy equity and invoicing account
    /// </summary>
    /// <param name="accountId">Account identifier</param>
    /// <param name="request">Suspension details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>200 OK or error response</returns>
    [HttpPost("{accountId}/suspend")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendAccount(
        string accountId,
        [FromBody] SuspendAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _accountManager.SuspendAccountAsync(
            accountId, 
            request.SuspensionReason, 
            cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ACCOUNT_NOT_FOUND" => NotFound(new { Error = result.ErrorMessage, ErrorCode = result.ErrorCode }),
                _ => BadRequest(new { Error = result.ErrorMessage, ErrorCode = result.ErrorCode })
            };
        }

        return Ok(new
        {
            Message = "Account suspended successfully",
            AccountId = result.AccountId
        });
    }

    /// <summary>
    /// Closes a policy equity and invoicing account
    /// </summary>
    /// <param name="accountId">Account identifier</param>
    /// <param name="request">Closure details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>200 OK or error response</returns>
    [HttpPost("{accountId}/close")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloseAccount(
        string accountId,
        [FromBody] CloseAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _accountManager.CloseAccountAsync(
            accountId, 
            request.ClosureReason, 
            cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ACCOUNT_NOT_FOUND" => NotFound(new { Error = result.ErrorMessage, ErrorCode = result.ErrorCode }),
                _ => BadRequest(new { Error = result.ErrorMessage, ErrorCode = result.ErrorCode })
            };
        }

        return Ok(new
        {
            Message = "Account closed successfully",
            AccountId = result.AccountId
        });
    }

    /// <summary>
    /// Updates the invoicing cycle for an account
    /// </summary>
    /// <param name="accountId">Account identifier</param>
    /// <param name="request">Invoicing cycle update details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>200 OK or error response</returns>
    [HttpPut("{accountId}/billing-cycle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePolicyEquityAndInvoicingCycle(
        string accountId,
        [FromBody] UpdatePolicyEquityAndInvoicingCycleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var dto = new UpdatePolicyEquityAndInvoicingCycleDto
        {
            AccountId = accountId,
            NewPolicyEquityAndInvoicingCycle = request.NewPolicyEquityAndInvoicingCycle,
            ChangeReason = request.ChangeReason
        };

        var result = await _accountManager.UpdatePolicyEquityAndInvoicingCycleAsync(dto, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ACCOUNT_NOT_FOUND" => NotFound(new { Error = result.ErrorMessage, ErrorCode = result.ErrorCode }),
                _ => BadRequest(new { Error = result.ErrorMessage, ErrorCode = result.ErrorCode })
            };
        }

        return Ok(new
        {
            Message = "Invoicing cycle updated successfully",
            AccountId = result.AccountId,
            NewPolicyEquityAndInvoicingCycle = request.NewPolicyEquityAndInvoicingCycle
        });
    }

    /// <summary>
    /// Retrieves all policy equity and invoicing accounts
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>200 OK with list of accounts (empty list if none exist)</returns>
    /// <response code="200">List of accounts retrieved successfully</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PolicyEquityAndInvoicingAccountResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAccounts(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving all policy equity and invoicing accounts");

        var accounts = await _accountManager.GetAllAccountsAsync(cancellationToken);

        var response = accounts.Select(account => new PolicyEquityAndInvoicingAccountResponse
        {
            AccountId = account.AccountId,
            CustomerId = account.CustomerId,
            PolicyNumber = account.PolicyNumber,
            PolicyHolderName = account.PolicyHolderName,
            CurrentPremiumOwed = account.CurrentPremiumOwed,
            TotalPaid = account.TotalPaid,
            OutstandingBalance = account.OutstandingBalance,
            Status = account.Status.ToString(),
            PolicyEquityAndInvoicingCycle = account.PolicyEquityAndInvoicingCycle.ToString(),
            EffectiveDate = account.EffectiveDate,
            CreatedUtc = account.CreatedUtc,
            LastUpdatedUtc = account.LastUpdatedUtc
        }).ToList();

        _logger.LogInformation("Retrieved {Count} policy equity and invoicing accounts", response.Count);

        return Ok(response);
    }

    /// <summary>
    /// Retrieves a single policy equity and invoicing account by ID
    /// </summary>
    /// <param name="accountId">Account identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>200 OK with account details, or 404 if not found</returns>
    /// <response code="200">Account retrieved successfully</response>
    /// <response code="404">Account not found</response>
    [HttpGet("{accountId}")]
    [ProducesResponseType(typeof(PolicyEquityAndInvoicingAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccount(string accountId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving policy equity and invoicing account {AccountId}", accountId);

        var account = await _accountManager.GetAccountByIdAsync(accountId, cancellationToken);

        if (account == null)
        {
            _logger.LogInformation("Policy equity and invoicing account {AccountId} not found", accountId);
            return NotFound(new { Error = "Account not found", AccountId = accountId });
        }

        var response = new PolicyEquityAndInvoicingAccountResponse
        {
            AccountId = account.AccountId,
            CustomerId = account.CustomerId,
            PolicyNumber = account.PolicyNumber,
            PolicyHolderName = account.PolicyHolderName,
            CurrentPremiumOwed = account.CurrentPremiumOwed,
            TotalPaid = account.TotalPaid,
            OutstandingBalance = account.OutstandingBalance,
            Status = account.Status.ToString(),
            PolicyEquityAndInvoicingCycle = account.PolicyEquityAndInvoicingCycle.ToString(),
            EffectiveDate = account.EffectiveDate,
            CreatedUtc = account.CreatedUtc,
            LastUpdatedUtc = account.LastUpdatedUtc
        };

        _logger.LogInformation("Retrieved policy equity and invoicing account {AccountId}", accountId);

        return Ok(response);
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    /// <returns>200 OK if service is healthy</returns>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Service = "PolicyEquityAndInvoicing Accounts API" });
    }
}
