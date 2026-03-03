using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiskInsure.FileRetrieval.API.Models;
using RiskInsure.FileRetrieval.Application.Services;
using RiskInsure.FileRetrieval.Domain.Enums;
using System.Security.Claims;

namespace RiskInsure.FileRetrieval.API.Controllers;

/// <summary>
/// T119: API controller for querying file retrieval execution history.
/// Provides read-only access to execution records for monitoring and diagnostics.
/// </summary>
[ApiController]
[Route("api/v1/configuration/{configurationId}/[controller]")]
[Authorize(Policy = "ClientAccess")]
public class ExecutionHistoryController : ControllerBase
{
    private readonly ExecutionHistoryService _executionHistoryService;
    private readonly ConfigurationService _configurationService;
    private readonly ILogger<ExecutionHistoryController> _logger;

    public ExecutionHistoryController(
        ExecutionHistoryService executionHistoryService,
        ConfigurationService configurationService,
        ILogger<ExecutionHistoryController> logger)
    {
        _executionHistoryService = executionHistoryService;
        _configurationService = configurationService;
        _logger = logger;
    }

    private string GetClientIdFromClaims()
    {
        var clientId = User.FindFirst("client_id")?.Value;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("client_id claim not found in JWT token");
            throw new UnauthorizedAccessException("client_id claim is required but not found in token");
        }
        return clientId;
    }

    /// <summary>
    /// T121: Gets execution history for a configuration with optional filtering.
    /// T123: Supports filtering by status (Completed, Failed) and date range.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedExecutionHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExecutionHistory(
        Guid configurationId,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? continuationToken = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var clientId = GetClientIdFromClaims();

            // Verify configuration exists and belongs to client
            var configuration = await _configurationService.GetByIdAsync(
                clientId,
                configurationId,
                cancellationToken);

            if (configuration == null)
            {
                return NotFound(new { error = "Configuration not found" });
            }

            // Validate page size
            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new { error = "Page size must be between 1 and 100" });
            }

            // Parse status filter
            ExecutionStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<ExecutionStatus>(status, ignoreCase: true, out var parsedStatus))
                {
                    statusFilter = parsedStatus;
                }
                else
                {
                    return BadRequest(new { error = $"Invalid status value: {status}. Valid values are: Pending, Running, Completed, Failed" });
                }
            }

            _logger.LogInformation(
                "Getting execution history for configuration {ConfigurationId}, client {ClientId}",
                configurationId,
                clientId);

            var result = await _executionHistoryService.GetExecutionHistoryAsync(
                clientId,
                configurationId,
                pageSize,
                continuationToken,
                statusFilter,
                startDate,
                endDate,
                cancellationToken);

            var response = new PaginatedExecutionHistoryResponse
            {
                Executions = result.Executions.Select(e => new ExecutionHistoryResponse
                {
                    Id = e.Id,
                    ConfigurationId = e.ConfigurationId,
                    ConfigurationName = configuration.Name,
                    Status = e.Status.ToString(),
                    ExecutionStartedAt = e.ExecutionStartedAt,
                    ExecutionCompletedAt = e.ExecutionCompletedAt,
                    DurationMs = e.DurationMs,
                    FilesFound = e.FilesFound,
                    EventsPublished = e.FilesProcessed, // Map FilesProcessed to EventsPublished
                    CommandsSent = 0, // Not tracked in entity yet
                    ErrorMessage = e.ErrorMessage,
                    ErrorCategory = e.ErrorCategory // T127: Error categorization
                }).ToList(),
                ContinuationToken = result.ContinuationToken,
                HasMore = !string.IsNullOrEmpty(result.ContinuationToken),
                Count = result.Executions.Count
            };

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving execution history for configuration {ConfigurationId}", configurationId);
            return StatusCode(500, new { error = "An error occurred while retrieving execution history" });
        }
    }

    /// <summary>
    /// T125: Gets details for a single execution.
    /// T126: Includes discovered files list with file metadata.
    /// </summary>
    [HttpGet("{executionId}")]
    [ProducesResponseType(typeof(ExecutionDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetExecutionDetails(
        Guid configurationId,
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var clientId = GetClientIdFromClaims();

            // Verify configuration exists
            var configuration = await _configurationService.GetByIdAsync(
                clientId,
                configurationId,
                cancellationToken);

            if (configuration == null)
            {
                return NotFound(new { error = "Configuration not found" });
            }

            _logger.LogDebug(
                "Getting execution details for execution {ExecutionId}, configuration {ConfigurationId}",
                executionId,
                configurationId);

            var execution = await _executionHistoryService.GetExecutionDetailsAsync(
                clientId,
                configurationId,
                executionId,
                cancellationToken);

            if (execution == null)
            {
                return NotFound(new { error = "Execution not found" });
            }

            // T126: Map discovered files to response - entity doesn't have DiscoveredFileIds yet
            var discoveredFiles = new List<DiscoveredFileInfo>(); // TODO: Query from DiscoveredFileRepository

            var response = new ExecutionDetailsResponse
            {
                Id = execution.Id,
                ConfigurationId = execution.ConfigurationId,
                ConfigurationName = configuration.Name,
                Status = execution.Status.ToString(),
                ExecutionStartedAt = execution.ExecutionStartedAt,
                ExecutionCompletedAt = execution.ExecutionCompletedAt,
                DurationMs = execution.DurationMs,
                FilesFound = execution.FilesFound,
                EventsPublished = execution.FilesProcessed, // Map FilesProcessed to EventsPublished
                CommandsSent = 0, // Not tracked in entity yet
                ErrorMessage = execution.ErrorMessage,
                ErrorCategory = execution.ErrorCategory,
                DiscoveredFiles = discoveredFiles
            };

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving execution details for execution {ExecutionId}", executionId);
            return StatusCode(500, new { error = "An error occurred while retrieving execution details" });
        }
    }

    /// <summary>
    /// T128: Gets aggregated execution metrics for a configuration.
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(ExecutionMetricsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetExecutionMetrics(
        Guid configurationId,
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var clientId = GetClientIdFromClaims();

            // Verify configuration exists
            var configuration = await _configurationService.GetByIdAsync(
                clientId,
                configurationId,
                cancellationToken);

            if (configuration == null)
            {
                return NotFound(new { error = "Configuration not found" });
            }

            _logger.LogInformation(
                "Getting execution metrics for configuration {ConfigurationId}",
                configurationId);

            var metrics = await _executionHistoryService.GetExecutionMetricsAsync(
                clientId,
                configurationId,
                startDate,
                endDate,
                cancellationToken);

            var response = new ExecutionMetricsResponse
            {
                ConfigurationId = metrics.ConfigurationId,
                StartDate = metrics.StartDate,
                EndDate = metrics.EndDate,
                TotalExecutions = metrics.TotalExecutions,
                SuccessfulExecutions = metrics.SuccessfulExecutions,
                FailedExecutions = metrics.FailedExecutions,
                SuccessRate = metrics.SuccessRate,
                AverageDurationSeconds = metrics.AverageDuration.TotalSeconds,
                TotalFilesDiscovered = metrics.TotalFilesDiscovered,
                FilesDiscoveredPerDay = metrics.FilesDiscoveredPerDay
            };

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving execution metrics for configuration {ConfigurationId}", configurationId);
            return StatusCode(500, new { error = "An error occurred while retrieving execution metrics" });
        }
    }

    [HttpGet("processedfiles")]
    [ProducesResponseType(typeof(List<ProcessedFileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProcessedFiles(
        Guid configurationId,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? fileName = null,
        [FromQuery] Guid? executionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var clientId = GetClientIdFromClaims();

            var configuration = await _configurationService.GetByIdAsync(
                clientId,
                configurationId,
                cancellationToken);

            if (configuration == null)
            {
                return NotFound(new { error = "Configuration not found" });
            }

            if (pageSize < 1 || pageSize > 200)
            {
                return BadRequest(new { error = "Page size must be between 1 and 200" });
            }

            var processedRecords = await _executionHistoryService.GetProcessedFileRecordsAsync(
                clientId,
                configurationId,
                pageSize,
                fileName,
                executionId,
                cancellationToken);

            var response = processedRecords.Select(record => new ProcessedFileResponse
            {
                Id = record.Id,
                DiscoveredFileId = record.DiscoveredFileId,
                ExecutionId = record.ExecutionId,
                FileName = record.Filename,
                FileUrl = record.FileUrl,
                Protocol = record.Protocol,
                DownloadedSizeBytes = record.DownloadedSizeBytes,
                ChecksumAlgorithm = record.ChecksumAlgorithm,
                ChecksumHex = record.ChecksumHex,
                ProcessedAt = record.ProcessedAt
            }).ToList();

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving processed files for configuration {ConfigurationId}", configurationId);
            return StatusCode(500, new { error = "An error occurred while retrieving processed files" });
        }
    }
}
