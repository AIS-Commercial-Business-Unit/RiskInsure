namespace RiskInsure.Customer.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using RiskInsure.Customer.Api.Models;
using RiskInsure.Customer.Domain.Managers;

[ApiController]
[Route("api/customers")]
[Produces("application/json")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerManager _manager;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        ICustomerManager manager,
        ILogger<CustomersController> logger)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        try
        {
            var address = new Domain.Models.Address
            {
                Street = request.Address.Street,
                City = request.Address.City,
                State = request.Address.State,
                ZipCode = request.Address.ZipCode
            };

            var customer = await _manager.CreateCustomerAsync(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Phone,
                address,
                request.BirthDate);

            var response = MapToResponse(customer);

            _logger.LogInformation(
                "Customer {CustomerId} created successfully",
                customer.CustomerId);

            return CreatedAtAction(
                nameof(GetCustomer),
                new { customerId = customer.CustomerId },
                response);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(
                "Customer creation validation failed: {Errors}",
                string.Join(", ", ex.Errors.SelectMany(e => e.Value)));

            return BadRequest(new
            {
                error = "ValidationFailed",
                errors = ex.Errors
            });
        }
    }

    [HttpGet("{customerId}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomer(string customerId)
    {
        var customer = await _manager.GetCustomerAsync(customerId);
        if (customer == null)
        {
            _logger.LogWarning("Customer {CustomerId} not found", customerId);
            return NotFound();
        }

        var response = MapToResponse(customer);
        return Ok(response);
    }

    [HttpPut("{customerId}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCustomer(
        string customerId,
        [FromBody] UpdateCustomerRequest request)
    {
        try
        {
            var customer = await _manager.UpdateCustomerAsync(
                customerId,
                request.FirstName,
                request.LastName,
                request.PhoneNumber,
                request.MailingAddress);

            var response = MapToResponse(customer);

            _logger.LogInformation(
                "Customer {CustomerId} updated successfully",
                customerId);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Customer {CustomerId} not found for update", customerId);
            return NotFound();
        }
    }

    [HttpPost("{customerId}/change-email")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangeEmail(
        string customerId,
        [FromBody] ChangeEmailRequest request)
    {
        // Future implementation: email verification workflow
        _logger.LogInformation(
            "Email change request received for customer {CustomerId} to {NewEmail}",
            customerId, request.NewEmail);

        return Accepted(new
        {
            message = "Email change verification sent",
            customerId
        });
    }

    [HttpDelete("{customerId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCustomer(string customerId)
    {
        try
        {
            await _manager.CloseCustomerAsync(customerId);

            _logger.LogInformation(
                "Customer {CustomerId} closed successfully",
                customerId);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Customer {CustomerId} not found for deletion", customerId);
            return NotFound();
        }
    }

    private static CustomerResponse MapToResponse(Domain.Models.Customer customer)
    {
        return new CustomerResponse
        {
            CustomerId = customer.CustomerId,
            Email = customer.Email,
            BirthDate = customer.BirthDate,
            ZipCode = customer.ZipCode,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            PhoneNumber = customer.PhoneNumber,
            MailingAddress = customer.MailingAddress,
            Status = customer.Status,
            EmailVerified = customer.EmailVerified,
            CreatedUtc = customer.CreatedUtc,
            UpdatedUtc = customer.UpdatedUtc
        };
    }
}
