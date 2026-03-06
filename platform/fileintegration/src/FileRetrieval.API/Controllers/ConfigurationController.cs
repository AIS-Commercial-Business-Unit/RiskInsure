using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NServiceBus;
using RiskInsure.FileRetrieval.API.Models;
using RiskInsure.FileRetrieval.Application.Services;
using FileRetrieval.Contracts.Commands;
using FileRetrieval.Contracts.DTOs;
using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.ValueObjects;
using System.Security.Claims;

namespace RiskInsure.FileRetrieval.API.Controllers;

/// <summary>
/// API controller for managing file retrieval configurations.
/// All endpoints require JWT authentication with clientId claim.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ClientAccess")]
public class ConfigurationController : ControllerBase
{
    private readonly IMessageSession _messageSession;
    private readonly ConfigurationService _configurationService;
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(
        IMessageSession messageSession,
        ConfigurationService configurationService,
        ILogger<ConfigurationController> logger)
    {
        _messageSession = messageSession;
        _configurationService = configurationService;
        _logger = logger;
    }

    /// <summary>
    /// Extracts clientId from JWT claims.
    /// </summary>
    /// <returns>ClientId from authenticated user's JWT token</returns>
    private string GetClientIdFromClaims()
    {
        var clientId = User.FindFirst("client_id")?.Value;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("client_id claim not found in JWT token for user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            throw new UnauthorizedAccessException("client_id claim is required but not found in token");
        }
        return clientId;
    }

    /// <summary>
    /// Extracts user identifier from JWT claims.
    /// </summary>
    /// <returns>User identifier (sub claim or email)</returns>
    private string GetUserIdFromClaims()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.Email)?.Value
            ?? "unknown-user";
    }

    /// <summary>
    /// Creates a new file retrieval configuration.
    /// </summary>
    /// <param name="request">Configuration details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created configuration</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ConfigurationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateConfiguration(
        [FromBody] CreateConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract clientId from JWT claims (enforces client isolation)
            var clientId = GetClientIdFromClaims();
            var userId = GetUserIdFromClaims();

            var configurationId = Guid.NewGuid();
            var correlationId = $"{clientId}-{configurationId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

            _logger.LogInformation(
                "Creating configuration {ConfigurationId} for client {ClientId} by user {UserId}",
                configurationId,
                clientId,
                userId);

            // Send CreateConfiguration command via NServiceBus
            var command = new CreateConfiguration
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = correlationId,
                OccurredUtc = DateTimeOffset.UtcNow,
                IdempotencyKey = $"{clientId}:{configurationId}",
                ClientId = clientId,
                ConfigurationId = configurationId,
                Name = request.Name,
                Description = request.Description,
                Protocol = request.Protocol,
                ProtocolSettings = request.ProtocolSettings,
                FilePathPattern = request.FilePathPattern,
                FilenamePattern = request.FilenamePattern,
                FileExtension = request.FileExtension,
                Schedule = request.Schedule,
                CreatedBy = userId
            };

            await _messageSession.Send(command, cancellationToken);

            // For synchronous response, we need to query the configuration after command handling
            // In a real implementation, this might use a delayed query or accept eventual consistency
            // For now, return a 202 Accepted with location header
            
            _logger.LogInformation(
                "Configuration creation command sent for {ConfigurationId} by user {UserId}",
                configurationId,
                userId);

            return Accepted($"/api/configuration/{configurationId}", new
            {
                id = configurationId,
                clientId,
                message = "Configuration creation in progress"
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid configuration request for client {ClientId}", GetClientIdFromClaims());
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating configuration for client {ClientId}", GetClientIdFromClaims());
            return StatusCode(500, new { error = "An error occurred while creating the configuration" });
        }
    }

    /// <summary>
    /// Gets a configuration by ID.
    /// </summary>
    /// <param name="id">Configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Configuration details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ConfigurationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetConfiguration(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract clientId from JWT claims (enforces client isolation)
            var clientId = GetClientIdFromClaims();

            _logger.LogDebug(
                "Getting configuration {ConfigurationId} for client {ClientId}",
                id,
                clientId);

            var configuration = await _configurationService.GetByIdAsync(
                clientId,
                id,
                cancellationToken);

            if (configuration == null)
            {
                _logger.LogWarning(
                    "Configuration {ConfigurationId} not found for client {ClientId}",
                    id,
                    clientId);
                return NotFound(new { error = "Configuration not found" });
            }

            var response = MapEntityToResponse(configuration);
            
            _logger.LogDebug(
                "Successfully retrieved configuration {ConfigurationId} for client {ClientId}",
                id,
                clientId);
            
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt for configuration {ConfigurationId}", id);
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration {ConfigurationId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the configuration" });
        }
    }

    /// <summary>
    /// Gets all configurations for the current client.
    /// </summary>
    /// <param name="includeInactive">Whether to include inactive configurations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of configurations</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ConfigurationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetConfigurations(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract clientId from JWT claims (enforces client isolation)
            var clientId = GetClientIdFromClaims();

            _logger.LogInformation(
                "Getting configurations for client {ClientId} (includeInactive: {IncludeInactive})",
                clientId,
                includeInactive);

            var configurations = await _configurationService.GetByClientAsync(
                clientId,
                includeInactive,
                cancellationToken);

            var responses = configurations.Select(MapEntityToResponse);
            
            _logger.LogInformation(
                "Retrieved {Count} configurations for client {ClientId}",
                configurations.Count(),
                clientId);
            
            return Ok(responses);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to list configurations");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configurations");
            return StatusCode(500, new { error = "An error occurred while retrieving configurations" });
        }
    }

    /// <summary>
    /// Gets configurations for the current client with pagination, sorting, and filtering.
    /// Supports clients with 20+ configurations (US4 requirement).
    /// </summary>
    /// <param name="pageSize">Number of items per page (max 100)</param>
    /// <param name="continuationToken">Token for next page</param>
    /// <param name="protocol">Filter by protocol (FTP, HTTPS, AzureBlob)</param>
    /// <param name="isActive">Filter by active status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of configurations with continuation token</returns>
    [HttpGet("list")]
    [ProducesResponseType(typeof(PaginatedConfigurationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetConfigurationsPaginated(
        [FromQuery] int pageSize = 20,
        [FromQuery] string? continuationToken = null,
        [FromQuery] string? protocol = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate pageSize
            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new { error = "Page size must be between 1 and 100" });
            }

            // Extract clientId from JWT claims (enforces client isolation)
            var clientId = GetClientIdFromClaims();

            // Parse protocol filter
            Domain.Enums.ProtocolType? protocolFilter = null;
            if (!string.IsNullOrWhiteSpace(protocol))
            {
                if (Enum.TryParse<Domain.Enums.ProtocolType>(protocol, ignoreCase: true, out var parsedProtocol))
                {
                    protocolFilter = parsedProtocol;
                }
                else
                {
                    return BadRequest(new { error = $"Invalid protocol value: {protocol}. Valid values are: FTP, HTTPS, AzureBlob" });
                }
            }

            _logger.LogInformation(
                "Getting paginated configurations for client {ClientId} (pageSize: {PageSize}, protocol: {Protocol}, isActive: {IsActive})",
                clientId,
                pageSize,
                protocol,
                isActive);

            var result = await _configurationService.GetByClientWithPaginationAsync(
                clientId,
                pageSize,
                continuationToken,
                protocolFilter,
                isActive,
                cancellationToken);

            var response = new PaginatedConfigurationResponse
            {
                Configurations = result.Configurations.Select(MapEntityToResponse).ToList(),
                ContinuationToken = result.ContinuationToken,
                HasMore = !string.IsNullOrEmpty(result.ContinuationToken),
                Count = result.Configurations.Count
            };
            
            _logger.LogInformation(
                "Retrieved {Count} configurations for client {ClientId} (hasMore: {HasMore})",
                response.Count,
                clientId,
                response.HasMore);
            
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to list configurations");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paginated configurations");
            return StatusCode(500, new { error = "An error occurred while retrieving configurations" });
        }
    }

    /// <summary>
    /// Updates an existing file retrieval configuration.
    /// T110: PUT endpoint with ETag validation for optimistic concurrency.
    /// </summary>
    /// <param name="id">Configuration ID</param>
    /// <param name="request">Updated configuration details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated configuration</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ConfigurationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateConfiguration(
        Guid id,
        [FromBody] UpdateConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract clientId from JWT claims (enforces client isolation)
            var clientId = GetClientIdFromClaims();
            var userId = GetUserIdFromClaims();

            // Verify configuration exists and belongs to this client
            var existing = await _configurationService.GetByIdAsync(clientId, id, cancellationToken);
            if (existing == null)
            {
                _logger.LogWarning(
                    "Configuration {ConfigurationId} not found for client {ClientId}",
                    id,
                    clientId);
                return NotFound(new { error = "Configuration not found" });
            }

            var correlationId = $"{clientId}-{id}-update-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

            _logger.LogInformation(
                "Updating configuration {ConfigurationId} for client {ClientId} by user {UserId}",
                id,
                clientId,
                userId);

            // Send UpdateConfiguration command via NServiceBus
            var command = new UpdateConfiguration
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = correlationId,
                OccurredUtc = DateTimeOffset.UtcNow,
                IdempotencyKey = $"{clientId}:{id}:update:{request.ETag}",
                ClientId = clientId,
                ConfigurationId = id,
                ETag = request.ETag,
                Name = request.Name,
                Description = request.Description,
                Protocol = request.Protocol,
                ProtocolSettings = request.ProtocolSettings,
                FilePathPattern = request.FilePathPattern,
                FilenamePattern = request.FilenamePattern,
                FileExtension = request.FileExtension,
                Schedule = request.Schedule,
                LastModifiedBy = userId
            };

            await _messageSession.Send(command, cancellationToken);

            _logger.LogInformation(
                "Configuration update command sent for {ConfigurationId} by user {UserId}",
                id,
                userId);

            // Return updated configuration (eventual consistency - may not reflect immediately)
            return Accepted($"/api/configuration/{id}", new
            {
                id,
                clientId,
                message = "Configuration update in progress"
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("was modified by another request"))
        {
            // T115: ETag conflict handling (409 Conflict)
            _logger.LogWarning(ex, "ETag conflict updating configuration {ConfigurationId}", id);
            
            // Retrieve latest version to return updated ETag
            var clientId = GetClientIdFromClaims();
            var latest = await _configurationService.GetByIdAsync(clientId, id, cancellationToken);
            
            return Conflict(new
            {
                error = "Configuration was modified by another request",
                message = "Please retrieve the latest version and try again",
                latestETag = latest?.ETag
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid update request for configuration {ConfigurationId}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration {ConfigurationId}", id);
            return StatusCode(500, new { error = "An error occurred while updating the configuration" });
        }
    }

    /// <summary>
    /// Manually triggers a file check for an existing configuration.
    /// Sends ExecuteFileCheck command with IsManualTrigger=true.
    /// </summary>
    /// <param name="id">Configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Accepted with execution ID</returns>
    [HttpPost("{id}/trigger")]
    [ProducesResponseType(typeof(TriggerFileCheckResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TriggerFileCheck(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract clientId from JWT claims (enforces client isolation)
            var clientId = GetClientIdFromClaims();
            var userId = GetUserIdFromClaims();

            // Verify configuration exists and belongs to this client
            var existing = await _configurationService.GetByIdAsync(clientId, id, cancellationToken);
            if (existing == null)
            {
                _logger.LogWarning(
                    "Configuration {ConfigurationId} not found for client {ClientId}",
                    id,
                    clientId);
                return NotFound(new { error = "Configuration not found" });
            }

            // Validate configuration is active
            if (!existing.IsActive)
            {
                _logger.LogWarning(
                    "Configuration {ConfigurationId} is inactive and cannot be triggered",
                    id);
                return BadRequest(new { error = "Configuration is inactive and cannot be triggered" });
            }

            var executionId = Guid.NewGuid();
            var correlationId = $"{clientId}-{id}-trigger-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            var triggeredAt = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Manually triggering file check for configuration {ConfigurationId} (client: {ClientId}, user: {UserId}, execution: {ExecutionId})",
                id,
                clientId,
                userId,
                executionId);

            // Send ExecuteFileCheck command via NServiceBus
            var command = new ExecuteFileCheck
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = correlationId,
                OccurredUtc = triggeredAt,
                IdempotencyKey = $"{clientId}:{id}:manual:{executionId}",
                ClientId = clientId,
                ConfigurationId = id,
                ScheduledExecutionTime = triggeredAt,
                IsManualTrigger = true
            };

            await _messageSession.Send(command, cancellationToken);

            _logger.LogInformation(
                "File check command sent for configuration {ConfigurationId} (execution: {ExecutionId})",
                id,
                executionId);

            return Accepted(new TriggerFileCheckResponse
            {
                ConfigurationId = id,
                ExecutionId = executionId,
                TriggeredAt = triggeredAt,
                Message = "File check triggered successfully"
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering file check for configuration {ConfigurationId}", id);
            return StatusCode(500, new { error = "An error occurred while triggering the file check" });
        }
    }

    /// <summary>
    /// Soft-deletes a file retrieval configuration by marking it inactive.
    /// T111: DELETE endpoint with ETag validation.
    /// </summary>
    /// <param name="id">Configuration ID</param>
    /// <param name="etag">ETag for optimistic concurrency (from If-Match header)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteConfiguration(
        Guid id,
        [FromHeader(Name = "If-Match")] string? etag,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract clientId from JWT claims (enforces client isolation)
            var clientId = GetClientIdFromClaims();
            var userId = GetUserIdFromClaims();

            // Verify configuration exists and belongs to this client
            var existing = await _configurationService.GetByIdAsync(clientId, id, cancellationToken);
            if (existing == null)
            {
                _logger.LogWarning(
                    "Configuration {ConfigurationId} not found for client {ClientId}",
                    id,
                    clientId);
                return NotFound(new { error = "Configuration not found" });
            }

            // Validate ETag if provided
            if (!string.IsNullOrWhiteSpace(etag) && etag != existing.ETag)
            {
                _logger.LogWarning(
                    "ETag mismatch deleting configuration {ConfigurationId} (expected: {Expected}, got: {Got})",
                    id,
                    existing.ETag,
                    etag);
                return Conflict(new
                {
                    error = "Configuration was modified by another request",
                    message = "Please retrieve the latest version and try again",
                    latestETag = existing.ETag
                });
            }

            var correlationId = $"{clientId}-{id}-delete-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

            _logger.LogInformation(
                "Deleting configuration {ConfigurationId} for client {ClientId} by user {UserId}",
                id,
                clientId,
                userId);

            // Send DeleteConfiguration command via NServiceBus
            var command = new DeleteConfiguration
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = correlationId,
                OccurredUtc = DateTimeOffset.UtcNow,
                IdempotencyKey = $"{clientId}:{id}:delete:{existing.ETag}",
                ClientId = clientId,
                ConfigurationId = id,
                DeletedBy = userId,
                ETag = existing.ETag ?? string.Empty
            };

            await _messageSession.Send(command, cancellationToken);

            _logger.LogInformation(
                "Configuration deletion command sent for {ConfigurationId} by user {UserId}",
                id,
                userId);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting configuration {ConfigurationId}", id);
            return StatusCode(500, new { error = "An error occurred while deleting the configuration" });
        }
    }

    private ConfigurationResponse MapEntityToResponse(Domain.Entities.FileRetrievalConfiguration entity)
    {
        return new ConfigurationResponse
        {
            Id = entity.Id,
            ClientId = entity.ClientId,
            Name = entity.Name,
            Description = entity.Description,
            Protocol = entity.Protocol.ToString(),
            ProtocolSettings = SanitizeProtocolSettings(entity.ProtocolSettings),
            FilePathPattern = entity.FilePathPattern,
            FilenamePattern = entity.FilenamePattern,
            FileExtension = entity.FileExtension,
            Schedule = MapScheduleToDto(entity.Schedule),
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            LastModifiedAt = entity.LastModifiedAt,
            LastModifiedBy = entity.LastModifiedBy,
            LastExecutedAt = entity.LastExecutedAt,
            NextScheduledRun = entity.NextScheduledRun,
            ETag = entity.ETag
        };
    }

    private Dictionary<string, object> SanitizeProtocolSettings(ProtocolSettings settings)
    {
        var sanitized = new Dictionary<string, object>();

        switch (settings)
        {
            case FtpProtocolSettings ftp:
                sanitized["Server"] = ftp.Server;
                sanitized["Port"] = ftp.Port;
                sanitized["Username"] = ftp.Username;
                sanitized["PasswordKeyVaultSecret"] = "[REDACTED]";
                sanitized["UseTls"] = ftp.UseTls;
                sanitized["UsePassiveMode"] = ftp.UsePassiveMode;
                sanitized["ConnectionTimeoutSeconds"] = (int)ftp.ConnectionTimeout.TotalSeconds;
                break;

            case HttpsProtocolSettings https:
                sanitized["BaseUrl"] = https.BaseUrl;
                sanitized["AuthenticationType"] = https.AuthenticationType.ToString();
                sanitized["UsernameOrApiKey"] = https.UsernameOrApiKey ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(https.PasswordOrTokenKeyVaultSecret))
                {
                    sanitized["PasswordOrTokenKeyVaultSecret"] = "[REDACTED]";
                }
                sanitized["ConnectionTimeoutSeconds"] = (int)https.ConnectionTimeout.TotalSeconds;
                sanitized["FollowRedirects"] = https.FollowRedirects;
                sanitized["MaxRedirects"] = https.MaxRedirects;
                break;

            case AzureBlobProtocolSettings azure:
                sanitized["StorageAccountName"] = azure.StorageAccountName;
                sanitized["ContainerName"] = azure.ContainerName;
                sanitized["AuthenticationType"] = azure.AuthenticationType.ToString();
                if (!string.IsNullOrWhiteSpace(azure.ConnectionStringKeyVaultSecret))
                {
                    sanitized["ConnectionStringKeyVaultSecret"] = "[REDACTED]";
                }
                if (!string.IsNullOrWhiteSpace(azure.SasTokenKeyVaultSecret))
                {
                    sanitized["SasTokenKeyVaultSecret"] = "[REDACTED]";
                }
                break;
        }

        return sanitized;
    }

    private ScheduleDefinitionDto MapScheduleToDto(ScheduleDefinition schedule)
    {
        return new ScheduleDefinitionDto
        {
            CronExpression = schedule.CronExpression,
            Timezone = schedule.Timezone,
            Description = schedule.Description
        };
    }
}
