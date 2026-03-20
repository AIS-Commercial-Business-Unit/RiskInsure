using NServiceBus;
using FileProcessing.Contracts.Commands;
using FileProcessing.Contracts.Events;
using RiskInsure.FileProcessing.Application.Services;
using RiskInsure.FileProcessing.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Blobs;
using System.Security.Cryptography;
using System.Runtime.InteropServices.Marshalling;

namespace RiskInsure.FileProcessing.Application.MessageHandlers;

/// <summary>
/// Handles RetrieveFile command by delegating to RetrieveFileService.
/// Downloads discovered files, stores them in blob storage, and sends ParseDiscoveredFile commands.
/// Thin handler pattern - validates message, delegates to service, publishes events.
/// </summary>
public class RetrieveFileHandler : IHandleMessages<RetrieveFile>
{
    private readonly RetrieveFileService _RetrieveFileService;
    private readonly DiscoveredFileContentDownloadService _discoveredFileContentDownloadService;
    private readonly IFileProcessingConfigurationRepository _configurationRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RetrieveFileHandler> _logger;

    public RetrieveFileHandler(
        RetrieveFileService RetrieveFileService,
        DiscoveredFileContentDownloadService discoveredFileContentDownloadService,
        IFileProcessingConfigurationRepository configurationRepository,
        IConfiguration configuration,
        ILogger<RetrieveFileHandler> logger)
    {
        _RetrieveFileService = RetrieveFileService ?? throw new ArgumentNullException(nameof(RetrieveFileService));
        _discoveredFileContentDownloadService = discoveredFileContentDownloadService ?? throw new ArgumentNullException(nameof(discoveredFileContentDownloadService));
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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

            // Execute file check via service to discover files
            var result = await _RetrieveFileService.CheckForFilesAsync(
                configuration,
                message.ScheduledExecutionTime,
                executionId,
                CancellationToken.None);

            if (result.Success)
            {
                // Process discovered files: download, store to blob storage, and send parse commands
                if (result.DiscoveredFiles.Any())
                {
                    await _RetrieveFileService.DownloadDiscoveredFilesAsync(
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
                    FilesProcessed = 0, // Not used anymore - ParseDiscoveredFileHandler will count processed
                    DurationMs = result.DurationMs,
                    ResolvedFilePathPattern = result.ResolvedFilePathPattern ?? string.Empty,
                    ResolvedFilenamePattern = result.ResolvedFilenamePattern ?? string.Empty
                });

                _logger.LogInformation(
                    "File check completed for configuration {ConfigurationId}: {FilesFound} files discovered (CorrelationId: {CorrelationId})",
                    message.ConfigurationId,
                    result.DiscoveredFiles.Count,
                    message.CorrelationId);
            }
            else
            {
                // Publish RetrieveFileFailed event (T090)
                await context.Publish(new RetrieveFileFailed
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

/// <summary>
/// Placeholder for discovered file information (used for type safety in the handler).
/// </summary>
public class DiscoveredFileInfo
{
    public string FileUrl { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public DateTimeOffset DiscoveredAt { get; set; }
}
