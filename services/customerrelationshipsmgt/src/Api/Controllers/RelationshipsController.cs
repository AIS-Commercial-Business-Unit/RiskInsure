namespace RiskInsure.CustomerRelationshipsMgt.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using RiskInsure.CustomerRelationshipsMgt.Api.Models;
using RiskInsure.CustomerRelationshipsMgt.Domain.Managers;

[ApiController]
[Route("api/relationships")]
[Produces("application/json")]
public class RelationshipsController : ControllerBase
{
    private readonly IRelationshipManager _manager;
    private readonly ILogger<RelationshipsController> _logger;

    public RelationshipsController(
        IRelationshipManager manager,
        ILogger<RelationshipsController> logger)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    [ProducesResponseType(typeof(RelationshipResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRelationship([FromBody] CreateRelationshipRequest request)
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

            var relationship = await _manager.CreateRelationshipAsync(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Phone,
                address,
                request.BirthDate);

            var response = MapToResponse(relationship);

            _logger.LogInformation(
                "Relationship {RelationshipId} created successfully",
                relationship.CustomerId);

            return CreatedAtAction(
                nameof(GetRelationship),
                new { relationshipId = relationship.CustomerId },
                response);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(
                "Relationship creation validation failed: {Errors}",
                string.Join(", ", ex.Errors.SelectMany(e => e.Value)));

            return BadRequest(new
            {
                error = "ValidationFailed",
                errors = ex.Errors
            });
        }
    }

    [HttpGet("{relationshipId}")]
    [ProducesResponseType(typeof(RelationshipResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRelationship(string relationshipId)
    {
        var relationship = await _manager.GetRelationshipAsync(relationshipId);
        if (relationship == null)
        {
            _logger.LogWarning("Relationship {RelationshipId} not found", relationshipId);
            return NotFound();
        }

        var response = MapToResponse(relationship);
        return Ok(response);
    }

    [HttpPut("{relationshipId}")]
    [ProducesResponseType(typeof(RelationshipResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRelationship(
        string relationshipId,
        [FromBody] UpdateRelationshipRequest request)
    {
        try
        {
            var relationship = await _manager.UpdateRelationshipAsync(
                relationshipId,
                request.FirstName,
                request.LastName,
                request.PhoneNumber,
                request.MailingAddress);

            var response = MapToResponse(relationship);

            _logger.LogInformation(
                "Relationship {RelationshipId} updated successfully",
                relationshipId);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Relationship {RelationshipId} not found for update", relationshipId);
            return NotFound();
        }
    }

    [HttpPost("{relationshipId}/change-email")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangeEmail(
        string relationshipId,
        [FromBody] ChangeEmailRequest request)
    {
        // Future implementation: email verification workflow
        _logger.LogInformation(
            "Email change request received for relationship {RelationshipId} to {NewEmail}",
            relationshipId, request.NewEmail);

        return Accepted(new
        {
            message = "Email change verification sent",
            relationshipId
        });
    }

    [HttpDelete("{relationshipId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteRelationship(string relationshipId)
    {
        try
        {
            await _manager.CloseRelationshipAsync(relationshipId);

            _logger.LogInformation(
                "Relationship {RelationshipId} closed successfully",
                relationshipId);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Relationship {RelationshipId} not found for deletion", relationshipId);
            return NotFound();
        }
    }

    private static RelationshipResponse MapToResponse(Domain.Models.Relationship relationship)
    {
        return new RelationshipResponse
        {
            RelationshipId = relationship.CustomerId,
            Email = relationship.Email,
            BirthDate = relationship.BirthDate,
            ZipCode = relationship.ZipCode,
            FirstName = relationship.FirstName,
            LastName = relationship.LastName,
            PhoneNumber = relationship.PhoneNumber,
            MailingAddress = relationship.MailingAddress,
            Status = relationship.Status,
            EmailVerified = relationship.EmailVerified,
            CreatedUtc = relationship.CreatedUtc,
            UpdatedUtc = relationship.UpdatedUtc
        };
    }
}
