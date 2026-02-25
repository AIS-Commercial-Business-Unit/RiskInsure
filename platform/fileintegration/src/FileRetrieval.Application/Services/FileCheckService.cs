using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.Repositories;
using FileRetrieval.Application.Protocols;
using Microsoft.Extensions.Logging;
using NServiceBus;
using FileRetrieval.Contracts.Events;
using FileRetrieval.Contracts.Commands;

namespace RiskInsure.FileRetrieval.Application.Services;

/// <summary>
/// Service for executing file checks according to FileRetrievalConfiguration.
/// Orchestrates token replacement, protocol adapter invocation, and execution tracking.
/// </summary>
public class FileCheckService
{
    private readonly ProtocolAdapterFactory _protocolAdapterFactory;
    private readonly TokenReplacementService _tokenReplacementService;
    private readonly IFileRetrievalExecutionRepository _executionRepository;
    private readonly IDiscoveredFileRepository _discoveredFileRepository;
    private readonly ILogger<FileCheckService> _logger;

    // Error categories for structured error handling (T083)
    private const string ERROR_CATEGORY_AUTHENTICATION = "AuthenticationFailure";
    private const string ERROR_CATEGORY_CONNECTION_TIMEOUT = "ConnectionTimeout";
    private const string ERROR_CATEGORY_PROTOCOL_ERROR = "ProtocolError";
    private const string ERROR_CATEGORY_CONFIGURATION_ERROR = "ConfigurationError";
    private const string ERROR_CATEGORY_UNKNOWN = "UnknownError";

    // Retry configuration (T082)
    private const int MAX_RETRY_ATTEMPTS = 3;
    private static readonly TimeSpan[] RetryDelays = 
    {
        TimeSpan.FromSeconds(2),   // First retry after 2 seconds
        TimeSpan.FromSeconds(5),   // Second retry after 5 seconds
        TimeSpan.FromSeconds(10)   // Third retry after 10 seconds
    };

    public FileCheckService(
        ProtocolAdapterFactory protocolAdapterFactory,
        TokenReplacementService tokenReplacementService,
        IFileRetrievalExecutionRepository executionRepository,
        IDiscoveredFileRepository discoveredFileRepository,
        ILogger<FileCheckService> logger)
    {
        _protocolAdapterFactory = protocolAdapterFactory ?? throw new ArgumentNullException(nameof(protocolAdapterFactory));
        _tokenReplacementService = tokenReplacementService ?? throw new ArgumentNullException(nameof(tokenReplacementService));
        _executionRepository = executionRepository ?? throw new ArgumentNullException(nameof(executionRepository));
        _discoveredFileRepository = discoveredFileRepository ?? throw new ArgumentNullException(nameof(discoveredFileRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes a file check for the specified configuration.
    /// Handles token replacement, protocol adapter invocation, and execution tracking.
    /// Implements retry logic with exponential backoff for transient errors.
    /// </summary>
    /// <param name="configuration">Configuration to execute</param>
    /// <param name="executionDate">Date to use for token replacement (defaults to UtcNow)</param>
    /// <param name="executionId">Optional execution ID for tracking (generates new if not provided)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution result with discovered files</returns>
    public async Task<FileCheckResult> ExecuteCheckAsync(
        FileRetrievalConfiguration configuration,
        DateTimeOffset? executionDate = null,
        Guid? executionId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var effectiveDate = executionDate ?? DateTimeOffset.UtcNow;
        var effectiveExecutionId = executionId ?? Guid.NewGuid();
        var startTime = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Starting file check execution {ExecutionId} for configuration {ConfigurationId} (Client: {ClientId})",
            effectiveExecutionId,
            configuration.Id,
            configuration.ClientId);

        // Create execution record
        var execution = new FileRetrievalExecution
        {
            Id = effectiveExecutionId,
            ClientId = configuration.ClientId,
            ConfigurationId = configuration.Id,
            ExecutionStartedAt = startTime,
            Status = ExecutionStatus.InProgress,
            FilesFound = 0,
            FilesProcessed = 0,
            RetryCount = 0,
            ResolvedFilePathPattern = string.Empty,
            ResolvedFilenamePattern = string.Empty,
            DurationMs = 0,
            ETag = string.Empty
        };

        try
        {
            // Replace tokens in path and filename patterns
            var resolvedPath = _tokenReplacementService.ReplaceTokens(configuration.FilePathPattern, effectiveDate);
            var resolvedFilename = _tokenReplacementService.ReplaceTokens(configuration.FilenamePattern, effectiveDate);

            execution.ResolvedFilePathPattern = resolvedPath;
            execution.ResolvedFilenamePattern = resolvedFilename;

            _logger.LogDebug(
                "Token replacement completed for execution {ExecutionId}: Path='{ResolvedPath}', Filename='{ResolvedFilename}'",
                executionId,
                resolvedPath,
                resolvedFilename);

            // Create protocol adapter
            var adapter = _protocolAdapterFactory.CreateAdapter(configuration.Protocol, configuration.ProtocolSettings);

            // Extract server address from protocol settings
            var serverAddress = ExtractServerAddress(configuration.ProtocolSettings);

            // Execute file check with retry logic
            var discoveredFiles = await ExecuteWithRetryAsync(
                () => adapter.CheckForFilesAsync(
                    serverAddress,
                    resolvedPath,
                    resolvedFilename,
                    configuration.FileExtension,
                    cancellationToken),
                execution,
                cancellationToken);

            execution.FilesFound = discoveredFiles.Count();
            execution.Status = ExecutionStatus.Completed;
            execution.ExecutionCompletedAt = DateTimeOffset.UtcNow;
            execution.DurationMs = (long)(execution.ExecutionCompletedAt.Value - execution.ExecutionStartedAt).TotalMilliseconds;

            // Save execution record
            await _executionRepository.CreateAsync(execution, cancellationToken);

            _logger.LogInformation(
                "File check execution {ExecutionId} completed successfully: {FilesFound} files found in {DurationMs}ms",
                effectiveExecutionId,
                execution.FilesFound,
                execution.DurationMs);

            return new FileCheckResult
            {
                Success = true,
                ExecutionId = effectiveExecutionId,
                StartTime = startTime,
                DiscoveredFiles = discoveredFiles.ToList(),
                DurationMs = execution.DurationMs,
                ErrorMessage = null,
                ErrorCategory = null,
                ResolvedFilePathPattern = resolvedPath,
                ResolvedFilenamePattern = resolvedFilename
            };
        }
        catch (Exception ex)
        {
            var errorCategory = CategorizeError(ex);
            
            execution.Status = ExecutionStatus.Failed;
            execution.ExecutionCompletedAt = DateTimeOffset.UtcNow;
            execution.DurationMs = (long)(execution.ExecutionCompletedAt.Value - execution.ExecutionStartedAt).TotalMilliseconds;
            execution.ErrorMessage = ex.Message;
            execution.ErrorCategory = errorCategory;

            await _executionRepository.CreateAsync(execution, cancellationToken);

            _logger.LogError(
                ex,
                "File check execution {ExecutionId} failed with {ErrorCategory} after {RetryCount} retries: {ErrorMessage}",
                effectiveExecutionId,
                errorCategory,
                execution.RetryCount,
                ex.Message);

            return new FileCheckResult
            {
                Success = false,
                ExecutionId = effectiveExecutionId,
                StartTime = startTime,
                DiscoveredFiles = new List<DiscoveredFileInfo>(),
                DurationMs = execution.DurationMs,
                ErrorMessage = ex.Message,
                ErrorCategory = errorCategory,
                ResolvedFilePathPattern = execution.ResolvedFilePathPattern,
                ResolvedFilenamePattern = execution.ResolvedFilenamePattern
            };
        }
    }

    /// <summary>
    /// Executes operation with exponential backoff retry logic for transient errors.
    /// Implements T082 retry logic.
    /// </summary>
    private async Task<IEnumerable<DiscoveredFileInfo>> ExecuteWithRetryAsync(
        Func<Task<IEnumerable<DiscoveredFileInfo>>> operation,
        FileRetrievalExecution execution,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < MAX_RETRY_ATTEMPTS && IsTransientError(ex))
            {
                execution.RetryCount = attempt + 1;
                var delay = RetryDelays[attempt];

                _logger.LogWarning(
                    ex,
                    "File check attempt {Attempt} failed with transient error, retrying after {DelaySeconds}s: {ErrorMessage}",
                    attempt + 1,
                    delay.TotalSeconds,
                    ex.Message);

                await Task.Delay(delay, cancellationToken);
            }
        }

        // If we get here, all retries failed - let the exception propagate
        throw new InvalidOperationException("This should never be reached - retry logic error");
    }

    /// <summary>
    /// Determines if an exception represents a transient error that should be retried.
    /// </summary>
    private bool IsTransientError(Exception ex)
    {
        // Transient errors that should be retried:
        // - Timeout exceptions
        // - Network connectivity issues
        // - Temporary service unavailability
        
        var exceptionType = ex.GetType().Name;
        var message = ex.Message.ToLowerInvariant();

        return exceptionType.Contains("Timeout") ||
               exceptionType.Contains("SocketException") ||
               message.Contains("timeout") ||
               message.Contains("connection") ||
               message.Contains("network") ||
               message.Contains("temporarily unavailable") ||
               message.Contains("service unavailable");
    }

    /// <summary>
    /// Categorizes errors for structured monitoring and alerting (T083).
    /// </summary>
    private string CategorizeError(Exception ex)
    {
        var exceptionType = ex.GetType().Name;
        var message = ex.Message.ToLowerInvariant();

        // Authentication failures
        if (exceptionType.Contains("Authentication") ||
            exceptionType.Contains("Unauthorized") ||
            message.Contains("authentication") ||
            message.Contains("unauthorized") ||
            message.Contains("credentials") ||
            message.Contains("login failed"))
        {
            return ERROR_CATEGORY_AUTHENTICATION;
        }

        // Connection timeouts
        if (exceptionType.Contains("Timeout") ||
            message.Contains("timeout") ||
            message.Contains("timed out"))
        {
            return ERROR_CATEGORY_CONNECTION_TIMEOUT;
        }

        // Protocol-specific errors
        if (exceptionType.Contains("Ftp") ||
            exceptionType.Contains("Http") ||
            exceptionType.Contains("Blob") ||
            message.Contains("protocol") ||
            message.Contains("connection refused") ||
            message.Contains("host not found"))
        {
            return ERROR_CATEGORY_PROTOCOL_ERROR;
        }

        // Configuration errors
        if (exceptionType.Contains("Configuration") ||
            exceptionType.Contains("Argument") ||
            message.Contains("invalid configuration") ||
            message.Contains("missing setting"))
        {
            return ERROR_CATEGORY_CONFIGURATION_ERROR;
        }

        return ERROR_CATEGORY_UNKNOWN;
    }

    /// <summary>
    /// Extracts server address from protocol settings.
    /// </summary>
    private string ExtractServerAddress(Domain.ValueObjects.ProtocolSettings settings)
    {
        return settings switch
        {
            Domain.ValueObjects.FtpProtocolSettings ftp => ftp.Server,
            Domain.ValueObjects.HttpsProtocolSettings https => https.BaseUrl,
            Domain.ValueObjects.AzureBlobProtocolSettings azure => 
                $"https://{azure.StorageAccountName}.blob.core.windows.net",
            _ => throw new ArgumentException($"Unsupported protocol settings type: {settings.GetType().Name}")
        };
    }

    /// <summary>
    /// Processes discovered files with idempotency checks and publishes events/commands (T086, T087, T088).
    /// Implements zero-duplicate workflow triggers per SC-007.
    /// </summary>
    /// <param name="discoveredFiles">Files discovered during check</param>
    /// <param name="configuration">Configuration that triggered the discovery</param>
    /// <param name="executionId">Execution ID for correlation</param>
    /// <param name="messageContext">NServiceBus message context for publishing</param>
    /// <param name="correlationId">Correlation ID for distributed tracing (T093)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of files processed (events published)</returns>
    public async Task<int> ProcessDiscoveredFilesAsync(
        IEnumerable<DiscoveredFileInfo> discoveredFiles,
        FileRetrievalConfiguration configuration,
        Guid executionId,
        IMessageHandlerContext messageContext,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        int filesProcessed = 0;
        var discoveryDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);

        foreach (var fileInfo in discoveredFiles)
        {
            try
            {
                // T087: Idempotency check - prevent duplicate workflow triggers
                var exists = await _discoveredFileRepository.ExistsAsync(
                    configuration.ClientId,
                    configuration.Id,
                    fileInfo.FileUrl,
                    discoveryDate,
                    cancellationToken);

                if (exists)
                {
                    _logger.LogDebug(
                        "File {FileUrl} already discovered on {DiscoveryDate} - skipping duplicate (IdempotencyKey: {IdempotencyKey})",
                        fileInfo.FileUrl,
                        discoveryDate,
                        $"{configuration.ClientId}:{configuration.Id}:{fileInfo.FileUrl}:{discoveryDate}");
                    continue; // Skip duplicate
                }

                // Create DiscoveredFile record
                var discoveredFile = new DiscoveredFile
                {
                    Id = Guid.NewGuid(),
                    ClientId = configuration.ClientId,
                    ConfigurationId = configuration.Id,
                    ExecutionId = executionId,
                    FileUrl = fileInfo.FileUrl,
                    Filename = fileInfo.Filename,
                    FileSize = fileInfo.FileSize,
                    LastModified = fileInfo.LastModified,
                    DiscoveredAt = fileInfo.DiscoveredAt,
                    DiscoveryDate = discoveryDate,
                    Status = DiscoveryStatus.Discovered
                };

                // Save discovered file (enforces unique key constraint for idempotency)
                await _discoveredFileRepository.CreateAsync(discoveredFile, cancellationToken);

                // T086: Publish FileDiscovered events
                foreach (var eventDef in configuration.EventsToPublish)
                {
                    var fileDiscoveredEvent = new FileDiscovered
                    {
                        MessageId = Guid.NewGuid(),
                        CorrelationId = correlationId, // T093: Correlation ID propagation
                        OccurredUtc = DateTimeOffset.UtcNow,
                        IdempotencyKey = $"{configuration.ClientId}:{configuration.Id}:{fileInfo.FileUrl}:{discoveryDate}",
                        ClientId = configuration.ClientId,
                        ConfigurationId = configuration.Id,
                        ExecutionId = executionId,
                        DiscoveredFileId = discoveredFile.Id,
                        FileUrl = fileInfo.FileUrl,
                        Filename = fileInfo.Filename,
                        FileSize = fileInfo.FileSize,
                        LastModified = fileInfo.LastModified,
                        DiscoveredAt = fileInfo.DiscoveredAt,
                        ConfigurationName = configuration.Name,
                        Protocol = configuration.ProtocolSettings.ProtocolType.ToString(),
                        EventData = eventDef.EventData ?? new Dictionary<string, object>()
                    };

                    await messageContext.Publish(fileDiscoveredEvent);

                    _logger.LogInformation(
                        "Published FileDiscovered event for {Filename} (EventType: {EventType}, IdempotencyKey: {IdempotencyKey})",
                        fileInfo.Filename,
                        eventDef.EventType,
                        fileDiscoveredEvent.IdempotencyKey);
                }

                // T088: Send ProcessDiscoveredFile commands
                foreach (var commandDef in configuration.CommandsToSend ?? new List<Domain.ValueObjects.CommandDefinition>())
                {
                    var processFileCommand = new ProcessDiscoveredFile
                    {
                        MessageId = Guid.NewGuid(),
                        CorrelationId = correlationId, // T093: Correlation ID propagation
                        OccurredUtc = DateTimeOffset.UtcNow,
                        IdempotencyKey = $"{configuration.ClientId}:{configuration.Id}:{fileInfo.FileUrl}:{discoveryDate}:cmd",
                        ClientId = configuration.ClientId,
                        ConfigurationId = configuration.Id,
                        ExecutionId = executionId,
                        DiscoveredFileId = discoveredFile.Id,
                        FileUrl = fileInfo.FileUrl,
                        Filename = fileInfo.Filename,
                        FileSize = fileInfo.FileSize,
                        LastModified = fileInfo.LastModified,
                        DiscoveredAt = DateTimeOffset.UtcNow,
                        ConfigurationName = configuration.Name,
                        Protocol = configuration.ProtocolSettings.ProtocolType.ToString(),
                        CommandData = commandDef.CommandData ?? new Dictionary<string, object>()
                    };

                    await messageContext.Send(processFileCommand);

                    _logger.LogInformation(
                        "Sent ProcessDiscoveredFile command for {Filename} to {TargetEndpoint} (CommandType: {CommandType}, IdempotencyKey: {IdempotencyKey})",
                        fileInfo.Filename,
                        commandDef.TargetEndpoint,
                        commandDef.CommandType,
                        processFileCommand.IdempotencyKey);
                }

                // Update discovered file status
                discoveredFile.Status = DiscoveryStatus.EventPublished;
                discoveredFile.EventPublishedAt = DateTimeOffset.UtcNow;
                await _discoveredFileRepository.UpdateAsync(discoveredFile, cancellationToken);

                filesProcessed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing discovered file {FileUrl}: {ErrorMessage}",
                    fileInfo.FileUrl,
                    ex.Message);
                // Continue processing other files - don't fail entire batch
            }
        }

        _logger.LogInformation(
            "Processed {FilesProcessed} discovered files for configuration {ConfigurationId} (Execution: {ExecutionId})",
            filesProcessed,
            configuration.Id,
            executionId);

        return filesProcessed;
    }
}

/// <summary>
/// Result of a file check execution.
/// </summary>
public record FileCheckResult
{
    public required bool Success { get; init; }
    public required Guid ExecutionId { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public required List<DiscoveredFileInfo> DiscoveredFiles { get; init; }
    public required long DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCategory { get; init; }
    public string? ResolvedFilePathPattern { get; init; }
    public string? ResolvedFilenamePattern { get; init; }
}
