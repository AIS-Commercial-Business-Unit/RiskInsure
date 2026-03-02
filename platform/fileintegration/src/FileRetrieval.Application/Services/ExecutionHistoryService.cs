using Microsoft.Extensions.Logging;
using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.Repositories;

namespace RiskInsure.FileRetrieval.Application.Services;

/// <summary>
/// T120, T124: Service for querying file retrieval execution history.
/// Provides read-only access to execution records for monitoring and diagnostics.
/// </summary>
public class ExecutionHistoryService
{
    private readonly IFileRetrievalExecutionRepository _executionRepository;
    private readonly IDiscoveredFileRepository _discoveredFileRepository;
    private readonly IProcessedFileRecordRepository _processedRecordRepository;
    private readonly ILogger<ExecutionHistoryService> _logger;

    public ExecutionHistoryService(
        IFileRetrievalExecutionRepository executionRepository,
        IDiscoveredFileRepository discoveredFileRepository,
        IProcessedFileRecordRepository processedRecordRepository,
        ILogger<ExecutionHistoryService> logger)
    {
        _executionRepository = executionRepository;
        _discoveredFileRepository = discoveredFileRepository;
        _processedRecordRepository = processedRecordRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProcessedFileRecord>> GetProcessedFileRecordsAsync(
        string clientId,
        Guid configurationId,
        int pageSize = 50,
        string? fileName = null,
        Guid? executionId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Getting processed file records for configuration {ConfigurationId}, client {ClientId}",
            configurationId,
            clientId);

        return await _processedRecordRepository.GetByConfigurationAsync(
            clientId,
            configurationId,
            pageSize,
            fileName,
            executionId,
            cancellationToken);
    }

    /// <summary>
    /// T120: Gets execution history with pagination, filtering, and date range.
    /// </summary>
    public async Task<(IReadOnlyList<FileRetrievalExecution> Executions, string? ContinuationToken)> GetExecutionHistoryAsync(
        string clientId,
        Guid configurationId,
        int pageSize = 50,
        string? continuationToken = null,
        ExecutionStatus? statusFilter = null,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Getting execution history for configuration {ConfigurationId}, client {ClientId}",
            configurationId,
            clientId);

        try
        {
            var result = await _executionRepository.GetExecutionHistoryAsync(
                clientId,
                configurationId,
                pageSize,
                continuationToken,
                statusFilter,
                startDate,
                endDate,
                cancellationToken);

            _logger.LogInformation(
                "Retrieved {Count} executions for configuration {ConfigurationId} (hasMore: {HasMore})",
                result.Executions.Count,
                configurationId,
                !string.IsNullOrEmpty(result.ContinuationToken));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving execution history for configuration {ConfigurationId}",
                configurationId);
            throw;
        }
    }

    /// <summary>
    /// T124: Gets details for a single execution including discovered files.
    /// </summary>
    public async Task<FileRetrievalExecution?> GetExecutionDetailsAsync(
        string clientId,
        Guid configurationId,
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Getting execution details for execution {ExecutionId}, configuration {ConfigurationId}",
            executionId,
            configurationId);

        try
        {
            var execution = await _executionRepository.GetByIdAsync(
                clientId,
                configurationId,
                executionId,
                cancellationToken);

            if (execution == null)
            {
                _logger.LogWarning(
                    "Execution {ExecutionId} not found for configuration {ConfigurationId}",
                    executionId,
                    configurationId);
                return null;
            }

            _logger.LogInformation(
                "Retrieved execution details for {ExecutionId} (status: {Status}, filesFound: {FilesFound})",
                executionId,
                execution.Status,
                execution.FilesFound);

            return execution;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving execution details for {ExecutionId}",
                executionId);
            throw;
        }
    }

    /// <summary>
    /// T128: Gets aggregated execution metrics for a configuration.
    /// </summary>
    public async Task<ExecutionMetrics> GetExecutionMetricsAsync(
        string clientId,
        Guid configurationId,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Getting execution metrics for configuration {ConfigurationId}",
            configurationId);

        try
        {
            // Get all executions in date range
            var effectiveStartDate = startDate ?? DateTimeOffset.UtcNow.AddDays(-30);
            var effectiveEndDate = endDate ?? DateTimeOffset.UtcNow;

            var executions = await _executionRepository.GetByConfigurationAndDateRangeAsync(
                clientId,
                configurationId,
                effectiveStartDate,
                effectiveEndDate,
                cancellationToken);

            // Calculate metrics
            var totalExecutions = executions.Count;
            var successfulExecutions = executions.Count(e => e.Status == ExecutionStatus.Completed);
            var failedExecutions = executions.Count(e => e.Status == ExecutionStatus.Failed);
            var averageDuration = executions.Any()
                ? TimeSpan.FromTicks((long)executions.Average(e => e.DurationMs * TimeSpan.TicksPerMillisecond))
                : TimeSpan.Zero;
            var totalFilesDiscovered = executions.Sum(e => e.FilesFound);

            // Files discovered per day
            var daysInRange = (effectiveEndDate - effectiveStartDate).TotalDays;
            var filesPerDay = daysInRange > 0 ? totalFilesDiscovered / daysInRange : 0;

            var metrics = new ExecutionMetrics
            {
                ConfigurationId = configurationId,
                StartDate = effectiveStartDate,
                EndDate = effectiveEndDate,
                TotalExecutions = totalExecutions,
                SuccessfulExecutions = successfulExecutions,
                FailedExecutions = failedExecutions,
                SuccessRate = totalExecutions > 0 ? (double)successfulExecutions / totalExecutions : 0,
                AverageDuration = averageDuration,
                TotalFilesDiscovered = totalFilesDiscovered,
                FilesDiscoveredPerDay = filesPerDay
            };

            _logger.LogInformation(
                "Calculated metrics for configuration {ConfigurationId}: {SuccessRate:P0} success rate, {TotalExecutions} executions",
                configurationId,
                metrics.SuccessRate,
                totalExecutions);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error calculating execution metrics for configuration {ConfigurationId}",
                configurationId);
            throw;
        }
    }
}

/// <summary>
/// T128: Aggregated execution metrics for a configuration.
/// </summary>
public record ExecutionMetrics
{
    public required Guid ConfigurationId { get; init; }
    public required DateTimeOffset StartDate { get; init; }
    public required DateTimeOffset EndDate { get; init; }
    public required int TotalExecutions { get; init; }
    public required int SuccessfulExecutions { get; init; }
    public required int FailedExecutions { get; init; }
    public required double SuccessRate { get; init; }
    public required TimeSpan AverageDuration { get; init; }
    public required int TotalFilesDiscovered { get; init; }
    public required double FilesDiscoveredPerDay { get; init; }
}
