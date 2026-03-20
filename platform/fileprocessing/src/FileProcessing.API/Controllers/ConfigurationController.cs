using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NServiceBus;
using RiskInsure.FileProcessing.API.Models;
using RiskInsure.FileProcessing.Application.Services;
using FileProcessing.Contracts.Commands;
using FileProcessing.Contracts.DTOs;
using RiskInsure.FileProcessing.Domain.Enums;
using RiskInsure.FileProcessing.Domain.ValueObjects;
using Microsoft.Azure.Cosmos;
using System.Security.Claims;
using System.Text.Json;

namespace RiskInsure.FileProcessing.API.Controllers;

/// <summary>
/// API controller for managing file processing configurations.
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
    /// Creates a new file processing configuration.
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

            _logger.LogInformation(
                "Creating configuration {ConfigurationId} for client {ClientId} by user {UserId}",
                configurationId,
                clientId,
                userId);

            var configuration = MapCreateRequestToEntity(
                request,
                clientId,
                configurationId,
                userId,
                DateTimeOffset.UtcNow);

            var created = await _configurationService.CreateAsync(configuration, cancellationToken);

            _logger.LogInformation(
                "Successfully created configuration {ConfigurationId} for client {ClientId}",
                created.Id,
                created.ClientId);

            return CreatedAtAction(
                nameof(GetConfiguration),
                new { id = created.Id },
                MapEntityToResponse(created));
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
    /// Updates an existing file processing configuration.
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
                return NotFound(new { error = "Configuration not found" });
            }

            _logger.LogInformation(
                "Updating configuration {ConfigurationId} for client {ClientId} by user {UserId}",
                id,
                clientId,
                userId);

            ApplyUpdateRequestToEntity(existing, request, userId);
            var updated = await _configurationService.UpdateAsync(existing, cancellationToken);

            return Ok(MapEntityToResponse(updated));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            _logger.LogWarning(ex, "ETag conflict updating configuration {ConfigurationId}", id);
            
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
    /// Sends RetrieveFile command with IsManualTrigger=true.
    /// </summary>
    /// <param name="id">Configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Accepted with execution ID</returns>
    [HttpPost("{id}/trigger")]
    [ProducesResponseType(typeof(TriggerRetrieveFileResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TriggerRetrieveFile(
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

            // Send RetrieveFile command via NServiceBus
            var command = new RetrieveFile
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

            return Accepted(new TriggerRetrieveFileResponse
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
    /// Soft-deletes a file processing configuration by marking it inactive.
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

            _logger.LogInformation(
                "Deleting configuration {ConfigurationId} for client {ClientId} by user {UserId}",
                id,
                clientId,
                userId);

            var deleted = await _configurationService.DeleteAsync(
                clientId,
                id,
                userId,
                cancellationToken);

            if (!deleted)
            {
                return NotFound(new { error = "Configuration not found" });
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            _logger.LogWarning(ex, "ETag conflict deleting configuration {ConfigurationId}", id);

            var clientId = GetClientIdFromClaims();
            var latest = await _configurationService.GetByIdAsync(clientId, id, cancellationToken);

            return Conflict(new
            {
                error = "Configuration was modified by another request",
                message = "Please retrieve the latest version and try again",
                latestETag = latest?.ETag
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting configuration {ConfigurationId}", id);
            return StatusCode(500, new { error = "An error occurred while deleting the configuration" });
        }
    }

    private Domain.Entities.FileProcessingConfiguration MapCreateRequestToEntity(
        CreateConfigurationRequest request,
        string clientId,
        Guid configurationId,
        string createdBy,
        DateTimeOffset createdAt)
    {
        if (!Enum.TryParse<ProtocolType>(request.Protocol, ignoreCase: true, out var protocolType))
        {
            throw new ArgumentException($"Invalid protocol type: {request.Protocol}", nameof(request.Protocol));
        }

        return new Domain.Entities.FileProcessingConfiguration
        {
            Id = configurationId,
            ClientId = clientId,
            Name = request.Name,
            Description = request.Description,
            Protocol = protocolType,
            ProtocolSettings = MapProtocolSettings(protocolType, request.ProtocolSettings),
            FilePathPattern = request.FilePathPattern,
            FilenamePattern = request.FilenamePattern,
            FileExtension = request.FileExtension,
            Schedule = MapScheduleDefinition(request.Schedule),
            ProcessingConfig = new FileProcessingDefinition
            {
                FileType = request.ProcessingConfig.FileType
            },
            IsActive = true,
            CreatedAt = createdAt,
            CreatedBy = createdBy
        };
    }

    private void ApplyUpdateRequestToEntity(
        Domain.Entities.FileProcessingConfiguration existing,
        UpdateConfigurationRequest request,
        string lastModifiedBy)
    {
        if (!Enum.TryParse<ProtocolType>(request.Protocol, ignoreCase: true, out var protocolType))
        {
            throw new ArgumentException($"Invalid protocol type: {request.Protocol}", nameof(request.Protocol));
        }

        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.Protocol = protocolType;
        existing.ProtocolSettings = MapProtocolSettings(protocolType, request.ProtocolSettings);
        existing.FilePathPattern = request.FilePathPattern;
        existing.FilenamePattern = request.FilenamePattern;
        existing.FileExtension = request.FileExtension;
        existing.Schedule = MapScheduleDefinition(request.Schedule);
        existing.ProcessingConfig = new FileProcessingDefinition
        {
            FileType = request.ProcessingConfig.FileType
        };
        existing.LastModifiedBy = lastModifiedBy;
        existing.ETag = request.ETag;
    }

    private static ProtocolSettings MapProtocolSettings(ProtocolType protocolType, Dictionary<string, object> settings)
    {
        return protocolType switch
        {
            ProtocolType.FTP => new FtpProtocolSettings
            {
                Server = GetRequiredString(settings, "Server"),
                Port = GetInt(settings, "Port", 21),
                Username = GetRequiredString(settings, "Username"),
                Password = GetRequiredString(settings, "Password"),
                UseTls = GetBool(settings, "UseTls", true),
                UsePassiveMode = GetBool(settings, "UsePassiveMode", true),
                ConnectionTimeout = TimeSpan.FromSeconds(GetInt(settings, "ConnectionTimeoutSeconds", 30))
            },
            ProtocolType.HTTPS => new HttpsProtocolSettings
            {
                BaseUrl = GetRequiredString(settings, "BaseUrl"),
                AuthenticationType = ParseEnum(GetString(settings, "AuthenticationType"), AuthType.None),
                Username = GetString(settings, "Username"),
                PasswordOrTokenOrApiKey = GetString(settings, "PasswordOrTokenOrApiKey"),
                ConnectionTimeout = TimeSpan.FromSeconds(GetInt(settings, "ConnectionTimeoutSeconds", 30)),
                FollowRedirects = GetBool(settings, "FollowRedirects", true),
                MaxRedirects = GetInt(settings, "MaxRedirects", 3)
            },
            ProtocolType.AzureBlob => new AzureBlobProtocolSettings
            {
                StorageAccountName = GetRequiredString(settings, "StorageAccountName"),
                ContainerName = GetRequiredString(settings, "ContainerName"),
                AuthenticationType = ParseEnum(GetString(settings, "AuthenticationType"), AzureAuthType.ManagedIdentity),
                ConnectionString = GetString(settings, "ConnectionString"),
                SasToken = GetString(settings, "SasToken"),
                BlobPrefix = GetString(settings, "BlobPrefix")
            },
            _ => throw new ArgumentException($"Unsupported protocol type: {protocolType}")
        };
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum defaultValue) where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static string GetRequiredString(Dictionary<string, object> settings, string key)
    {
        return GetString(settings, key)
            ?? throw new ArgumentException($"Required setting '{key}' is missing or empty", key);
    }

    private static string? GetString(Dictionary<string, object> settings, string key)
    {
        if (!settings.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            JsonElement json when json.ValueKind == JsonValueKind.String => json.GetString(),
            JsonElement json => json.ToString(),
            _ => value.ToString()
        };
    }

    private static int GetInt(Dictionary<string, object> settings, string key, int defaultValue)
    {
        if (!settings.TryGetValue(key, out var value) || value is null)
        {
            return defaultValue;
        }

        return value switch
        {
            JsonElement json when json.ValueKind == JsonValueKind.Number => json.GetInt32(),
            JsonElement json when json.ValueKind == JsonValueKind.String && int.TryParse(json.GetString(), out var parsed) => parsed,
            _ when int.TryParse(value.ToString(), out var parsed) => parsed,
            _ => defaultValue
        };
    }

    private static bool GetBool(Dictionary<string, object> settings, string key, bool defaultValue)
    {
        if (!settings.TryGetValue(key, out var value) || value is null)
        {
            return defaultValue;
        }

        return value switch
        {
            JsonElement json when json.ValueKind is JsonValueKind.True or JsonValueKind.False => json.GetBoolean(),
            JsonElement json when json.ValueKind == JsonValueKind.String && bool.TryParse(json.GetString(), out var parsed) => parsed,
            _ when bool.TryParse(value.ToString(), out var parsed) => parsed,
            _ => defaultValue
        };
    }

    private static ScheduleDefinition MapScheduleDefinition(ScheduleDefinitionDto dto)
    {
        return new ScheduleDefinition(
            dto.CronExpression,
            dto.Timezone,
            dto.Description);
    }

    private ConfigurationResponse MapEntityToResponse(Domain.Entities.FileProcessingConfiguration entity)
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
            ProcessingConfig = new FileProcessingConfig
            {
                FileType = entity.ProcessingConfig?.FileType ?? string.Empty
            },
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
                sanitized["Password"] = "[REDACTED]";
                sanitized["UseTls"] = ftp.UseTls;
                sanitized["UsePassiveMode"] = ftp.UsePassiveMode;
                sanitized["ConnectionTimeoutSeconds"] = (int)ftp.ConnectionTimeout.TotalSeconds;
                break;

            case HttpsProtocolSettings https:
                sanitized["BaseUrl"] = https.BaseUrl;
                sanitized["AuthenticationType"] = https.AuthenticationType.ToString();
                sanitized["Username"] = https.Username ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(https.PasswordOrTokenOrApiKey))
                {
                    sanitized["PasswordOrTokenOrApiKey"] = "[REDACTED]";
                }
                sanitized["ConnectionTimeoutSeconds"] = (int)https.ConnectionTimeout.TotalSeconds;
                sanitized["FollowRedirects"] = https.FollowRedirects;
                sanitized["MaxRedirects"] = https.MaxRedirects;
                break;

            case AzureBlobProtocolSettings azure:
                sanitized["StorageAccountName"] = azure.StorageAccountName;
                sanitized["ContainerName"] = azure.ContainerName;
                sanitized["AuthenticationType"] = azure.AuthenticationType.ToString();
                if (!string.IsNullOrWhiteSpace(azure.ConnectionString))
                {
                    sanitized["ConnectionString"] = "[REDACTED]";
                }
                if (!string.IsNullOrWhiteSpace(azure.SasToken))
                {
                    sanitized["SasToken"] = "[REDACTED]";
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
