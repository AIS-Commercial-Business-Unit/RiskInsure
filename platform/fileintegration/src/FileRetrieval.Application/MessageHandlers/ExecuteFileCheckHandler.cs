using NServiceBus;
using FileRetrieval.Contracts.Commands;
using FileRetrieval.Contracts.Events;
using RiskInsure.FileRetrieval.Application.Services;
using RiskInsure.FileRetrieval.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace RiskInsure.FileRetrieval.Application.MessageHandlers;

/// <summary>
/// Handles ExecuteFileCheck command by delegating to FileCheckService.
/// Thin handler pattern - validates message, delegates to service, publishes events.
/// </summary>
public class ExecuteFileCheckHandler : IHandleMessages<ExecuteFileCheck>
{
    private readonly FileCheckService _fileCheckService;
    private readonly IFileRetrievalConfigurationRepository _configurationRepository;
    private readonly ILogger<ExecuteFileCheckHandler> _logger;

    public ExecuteFileCheckHandler(
        FileCheckService fileCheckService,
        IFileRetrievalConfigurationRepository configurationRepository,
        ILogger<ExecuteFileCheckHandler> logger)
    {
        _fileCheckService = fileCheckService ?? throw new ArgumentNullException(nameof(fileCheckService));
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ExecuteFileCheck message, IMessageHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogInformation(
            "Handling ExecuteFileCheck command for configuration {ConfigurationId} (Client: {ClientId}, CorrelationId: {CorrelationId})",
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
                
                // Publish FileCheckFailed event (T090)
                await context.Publish(new   FileCheckFailed
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

            // Publish FileCheckTriggered event (audit trail for both manual and scheduled triggers)
            await context.Publish(new FileCheckTriggered
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
                "FileCheckTriggered event published for configuration {ConfigurationId} (ExecutionId: {ExecutionId}, IsManual: {IsManual})",
                message.ConfigurationId,
                executionId,
                message.IsManualTrigger);

            // Execute file check via service
            var result = await _fileCheckService.ExecuteCheckAsync(
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
                    filesProcessed = await _fileCheckService.ProcessDiscoveredFilesAsync(
                        result.DiscoveredFiles,
                        configuration,
                        result.ExecutionId,
                        context,
                        message.CorrelationId,
                        CancellationToken.None);
                }

                // Publish FileCheckCompleted event (T089)
                await context.Publish(new FileCheckCompleted
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
                // Publish FileCheckFailed event (T090)
                await context.Publish(new   FileCheckFailed
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
                "Error handling ExecuteFileCheck command for configuration {ConfigurationId}",
                message.ConfigurationId);

            // Publish FileCheckFailed event
            await context.Publish(new   FileCheckFailed
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
