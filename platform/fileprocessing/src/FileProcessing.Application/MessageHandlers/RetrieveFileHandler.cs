using NServiceBus;
using FileProcessing.Contracts.Commands;
using FileProcessing.Contracts.Events;
using RiskInsure.FileProcessing.Application.Services;
using RiskInsure.FileProcessing.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace RiskInsure.FileProcessing.Application.MessageHandlers;

/// <summary>
/// Handles RetrieveFile command by delegating to RetrieveFileService.
/// Thin handler pattern - validates message, delegates to service, publishes events.
/// </summary>
public class RetrieveFileHandler : IHandleMessages<RetrieveFile>
{
    private readonly RetrieveFileService _RetrieveFileService;
    private readonly IFileProcessingConfigurationRepository _configurationRepository;
    private readonly ILogger<RetrieveFileHandler> _logger;

    public RetrieveFileHandler(
        RetrieveFileService RetrieveFileService,
        IFileProcessingConfigurationRepository configurationRepository,
        ILogger<RetrieveFileHandler> logger)
    {
        _RetrieveFileService = RetrieveFileService ?? throw new ArgumentNullException(nameof(RetrieveFileService));
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(RetrieveFile message, IMessageHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogInformation(
            "Handling RetrieveFile command for configuration {ConfigurationId} (Client: {ClientId}, CorrelationId: {CorrelationId})",
            message.ConfigurationId,
            message.ClientId,
            message.CorrelationId);

        try
        {
            // Load configuration
            var configuration = await _configurationRepository.GetByIdAsync(
                message.ClientId,
                message.ConfigurationId,
                CancellationToken.None);

            if (configuration == null)
            {
                _logger.LogWarning(
                    "Configuration {ConfigurationId} not found for client {ClientId}",
                    message.ConfigurationId,
                    message.ClientId);
                
                // Publish RetrieveFileFailed event (T090)
                await context.Publish(new RetrieveFileFailed
                {
                    MessageId = Guid.NewGuid(),
                    CorrelationId = message.CorrelationId,
                    OccurredUtc = DateTimeOffset.UtcNow,
                    IdempotencyKey = $"{message.ClientId}:{message.ConfigurationId}:failed:{DateTimeOffset.UtcNow.Ticks}",
                    ClientId = message.ClientId,
                    ConfigurationId = message.ConfigurationId,
                    ExecutionId = Guid.Empty,
                    ConfigurationName = "Unknown",
                    Protocol = "Unknown",
                    ExecutionStartedAt = DateTimeOffset.UtcNow,
                    ExecutionFailedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = "Configuration not found",
                    ErrorCategory = "ConfigurationError",
                    ResolvedFilePathPattern = "",
                    ResolvedFilenamePattern = "",
                    DurationMs = 0,
                    RetryCount = 0
                });
                
                return;
            }

            // Check if configuration is active
            if (!configuration.IsActive)
            {
                _logger.LogWarning(
                    "Configuration {ConfigurationId} is inactive, skipping file check",
                    message.ConfigurationId);
                return;
            }

            // Generate execution ID for this file check
            var executionId = Guid.NewGuid();

            // Publish RetrieveFileTriggered event (audit trail for both manual and scheduled triggers)
            await context.Publish(new RetrieveFileTriggered
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = message.CorrelationId,
                OccurredUtc = DateTimeOffset.UtcNow,
                IdempotencyKey = $"{message.ClientId}:{message.ConfigurationId}:triggered:{executionId}",
                ClientId = message.ClientId,
                ConfigurationId = message.ConfigurationId,
                ConfigurationName = configuration.Name,
                Protocol = configuration.ProtocolSettings.ProtocolType.ToString(),
                ExecutionId = executionId,
                ScheduledExecutionTime = message.ScheduledExecutionTime,
                IsManualTrigger = message.IsManualTrigger,
                TriggeredBy = message.IsManualTrigger ? "manual-api" : "scheduler"
            });

            _logger.LogInformation(
                "RetrieveFileTriggered event published for configuration {ConfigurationId} (ExecutionId: {ExecutionId}, IsManual: {IsManual})",
                message.ConfigurationId,
                executionId,
                message.IsManualTrigger);

            // Execute file check via service
            var result = await _RetrieveFileService.ExecuteCheckAsync(
                configuration,
                message.ScheduledExecutionTime,
                executionId,
                CancellationToken.None);

            if (result.Success)
            {
                // Process discovered files with idempotency checks and event publishing (T086, T087, T088)
                int filesProcessed = 0;
                if (result.DiscoveredFiles.Any())
                {
                    filesProcessed = await _RetrieveFileService.ParseDiscoveredFilesAsync(
                        result.DiscoveredFiles,
                        configuration,
                        result.ExecutionId,
                        context,
                        message.CorrelationId,
                        CancellationToken.None);
                }

                // Publish RetrieveFileCompleted event (T089)
                await context.Publish(new RetrieveFileCompleted
                {
                    MessageId = Guid.NewGuid(),
                    CorrelationId = message.CorrelationId,
                    OccurredUtc = DateTimeOffset.UtcNow,
                    IdempotencyKey = $"{message.ClientId}:{message.ConfigurationId}:completed:{result.ExecutionId}",
                    ClientId = message.ClientId,
                    ConfigurationId = message.ConfigurationId,
                    ExecutionId = result.ExecutionId,
                    ConfigurationName = configuration.Name,
                    Protocol = configuration.ProtocolSettings.ProtocolType.ToString(),
                    ExecutionStartedAt = result.StartTime,
                    ExecutionCompletedAt = DateTimeOffset.UtcNow,
                    FilesFound = result.DiscoveredFiles.Count,
                    FilesProcessed = filesProcessed,
                    DurationMs = result.DurationMs,
                    ResolvedFilePathPattern = result.ResolvedFilePathPattern ?? string.Empty,
                    ResolvedFilenamePattern = result.ResolvedFilenamePattern ?? string.Empty
                });

                _logger.LogInformation(
                    "File check completed for configuration {ConfigurationId}: {FilesFound} files discovered, {FilesProcessed} processed (CorrelationId: {CorrelationId})",
                    message.ConfigurationId,
                    result.DiscoveredFiles.Count,
                    filesProcessed,
                    message.CorrelationId);

                // File discovery event publishing will be handled in Phase 5 (US3)
                // For now, we just log the discovered files
                foreach (var file in result.DiscoveredFiles)
                {
                    _logger.LogDebug(
                        "Discovered file: {Filename} at {Url} (Size: {Size} bytes)",
                        file.Filename,
                        file.FileUrl,
                        file.FileSize);
                }
            }
            else
            {
                // Publish RetrieveFileFailed event (T090)
                await context.Publish(new   RetrieveFileFailed
                {
                    MessageId = Guid.NewGuid(),
                    CorrelationId = message.CorrelationId,
                    OccurredUtc = DateTimeOffset.UtcNow,
                    IdempotencyKey = $"{message.ClientId}:{message.ConfigurationId}:failed:{result.ExecutionId}",
                    ClientId = message.ClientId,
                    ConfigurationId = message.ConfigurationId,
                    ExecutionId = result.ExecutionId,
                    ConfigurationName = configuration.Name,
                    Protocol = configuration.ProtocolSettings.ProtocolType.ToString(),
                    ExecutionStartedAt = result.StartTime,
                    ExecutionFailedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = result.ErrorMessage ?? "Unknown error",
                    ErrorCategory = result.ErrorCategory ?? "UnknownError",
                    ResolvedFilePathPattern = result.ResolvedFilePathPattern ?? string.Empty,
                    ResolvedFilenamePattern = result.ResolvedFilenamePattern ?? string.Empty,
                    DurationMs = result.DurationMs,
                    RetryCount = 0 // Retrieved from execution record if needed
                });

                _logger.LogWarning(
                    "File check failed for configuration {ConfigurationId}: {ErrorMessage} ({ErrorCategory})",
                    message.ConfigurationId,
                    result.ErrorMessage,
                    result.ErrorCategory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error handling RetrieveFile command for configuration {ConfigurationId}",
                message.ConfigurationId);

            // Publish RetrieveFileFailed event
            await context.Publish(new   RetrieveFileFailed
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = message.CorrelationId,
                OccurredUtc = DateTimeOffset.UtcNow,
                IdempotencyKey = $"{message.ClientId}:{message.ConfigurationId}:failed:{DateTimeOffset.UtcNow.Ticks}",
                ClientId = message.ClientId,
                ConfigurationId = message.ConfigurationId,
                ExecutionId = Guid.Empty,
                ConfigurationName = "Unknown",
                Protocol = "Unknown",
                ExecutionStartedAt = DateTimeOffset.UtcNow,
                ExecutionFailedAt = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message,
                ErrorCategory = "HandlerError",
                ResolvedFilePathPattern = string.Empty,
                ResolvedFilenamePattern = string.Empty,
                DurationMs = 0,
                RetryCount = 0
            });

            throw; // Re-throw to trigger NServiceBus retry policy
        }
    }
}
