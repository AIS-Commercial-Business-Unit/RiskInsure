using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using RiskInsure.FileRetrieval.Domain.Repositories;
using RiskInsure.FileRetrieval.Infrastructure.Configuration;
using FileRetrieval.Contracts.Commands;
using System.Collections.Concurrent;
using System.Threading;

namespace RiskInsure.FileRetrieval.Infrastructure.Scheduling;

/// <summary>
/// Background service that periodically checks for scheduled file retrieval configurations
/// and triggers ExecuteFileCheck commands when schedules are due.
/// Polling interval is configurable via SchedulerOptions.
/// </summary>
public class SchedulerHostedService : BackgroundService
{
    private const string ExecuteFileCheckDestination = "FileRetrieval.Worker";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScheduleEvaluator _scheduleEvaluator;
    private readonly IMessageSession _messageSession;
    private readonly ILogger<SchedulerHostedService> _logger;
    private readonly SchedulerOptions _options;
    
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _executionWindow;
    private readonly int _maxConcurrentChecks;
    
    // Semaphore to limit concurrent file checks
    private readonly SemaphoreSlim _concurrencyLimiter;
    
    // Track in-progress configurations to prevent duplicates within this instance
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _inProgressChecks;

    public SchedulerHostedService(
        IServiceScopeFactory scopeFactory,
        ScheduleEvaluator scheduleEvaluator,
        IMessageSession messageSession,
        IOptions<SchedulerOptions> options,
        ILogger<SchedulerHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _scheduleEvaluator = scheduleEvaluator ?? throw new ArgumentNullException(nameof(scheduleEvaluator));
        _messageSession = messageSession ?? throw new ArgumentNullException(nameof(messageSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        
        // Get configuration values
        _checkInterval = _options.GetPollingInterval();
        _executionWindow = _options.GetExecutionWindow();
        _maxConcurrentChecks = _options.MaxConcurrentChecks;
        
        // Initialize concurrency control
        _concurrencyLimiter = new SemaphoreSlim(_maxConcurrentChecks, _maxConcurrentChecks);
        _inProgressChecks = new ConcurrentDictionary<Guid, DateTimeOffset>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SchedulerHostedService starting - polling interval: {PollingInterval}, max concurrent: {MaxConcurrent}, execution window: {ExecutionWindow}",
            _checkInterval,
            _maxConcurrentChecks,
            _executionWindow);

        // Wait a few seconds before starting to allow infrastructure to initialize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckScheduledConfigurationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking scheduled configurations: {ErrorMessage}", ex.Message);
            }

            // Wait for next check interval (configurable)
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
        }

        _logger.LogInformation("SchedulerHostedService stopping");
    }

    /// <summary>
    /// Checks all active configurations and sends ExecuteFileCheck commands for those due to run.
    /// </summary>
    private async Task CheckScheduledConfigurationsAsync(CancellationToken cancellationToken)
    {
        var checkTime = DateTimeOffset.UtcNow;
        
        _logger.LogDebug("Checking for scheduled file retrieval configurations at {CheckTime}", checkTime);

        try
        {
            // Get all active configurations
            // Note: This is a simplified implementation. In production, you might want to:
            // 1. Use a secondary index on NextScheduledRun for efficient queries
            // 2. Implement pagination for large numbers of configurations
            // 3. Use a distributed lock to prevent multiple instances from processing the same configuration

            using var scope = _scopeFactory.CreateScope();
            var configurationRepository = scope.ServiceProvider.GetRequiredService<IFileRetrievalConfigurationRepository>();
            var allConfigurations = await configurationRepository.GetAllActiveConfigurationsAsync(cancellationToken);

            _logger.LogDebug("Found {Count} active configurations to check", allConfigurations.Count());

            int triggeredCount = 0;
            int skippedCount = 0;
            int waitingCount = 0;

            // T100, T104: Process configurations with graceful failure handling
            var tasks = new List<Task>();
            
            foreach (var configuration in allConfigurations)
            {
                // T101: Skip if already in progress
                if (_inProgressChecks.ContainsKey(configuration.Id))
                {
                    skippedCount++;
                    _logger.LogDebug(
                        "Skipping configuration {ConfigurationId} - already in progress",
                        configuration.Id);
                    continue;
                }

                try
                {
                    // Calculate next execution time if not already set
                    var nextExecution = configuration.NextScheduledRun 
                        ?? _scheduleEvaluator.GetNextExecutionTime(configuration.Schedule, configuration.LastExecutedAt);

                    if (nextExecution == null)
                    {
                        _logger.LogWarning(
                            "Unable to calculate next execution time for configuration {ConfigurationId} (cron: '{CronExpression}')",
                            configuration.Id,
                            configuration.Schedule.CronExpression);
                        continue;
                    }

                    // Check if execution is due (within execution window)
                    var timeDifference = nextExecution.Value - checkTime;
                    
                    if (timeDifference.TotalMinutes <= _executionWindow.TotalMinutes && timeDifference.TotalSeconds >= 0)
                    {
                        // Wait for available slot (non-blocking)
                        if (_concurrencyLimiter.CurrentCount == 0)
                        {
                            waitingCount++;
                            _logger.LogWarning(
                                "Concurrent execution limit reached ({MaxConcurrent}). Configuration {ConfigurationId} will be deferred.",
                                _maxConcurrentChecks,
                                configuration.Id);
                            continue;
                        }

                        // T104: Launch check asynchronously so one failure doesn't affect others
                        var task = Task.Run(async () =>
                        {
                            await _concurrencyLimiter.WaitAsync(cancellationToken);
                            try
                            {
                                // T101: Mark as in-progress
                                _inProgressChecks.TryAdd(configuration.Id, DateTimeOffset.UtcNow);

                                await TriggerFileCheckAsync(configuration, nextExecution.Value, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                // T104: Log error but don't propagate to prevent affecting other configurations
                                _logger.LogError(
                                    ex,
                                    "Error triggering file check for configuration {ConfigurationId}: {ErrorMessage}",
                                    configuration.Id,
                                    ex.Message);
                            }
                            finally
                            {
                                // T101: Remove from in-progress
                                _inProgressChecks.TryRemove(configuration.Id, out _);
                                _concurrencyLimiter.Release();
                            }
                        }, cancellationToken);

                        tasks.Add(task);
                        triggeredCount++;
                    }
                    else if (timeDifference.TotalMinutes < 0)
                    {
                        // Execution is overdue - this might indicate a problem or missed execution
                        _logger.LogWarning(
                            "Configuration {ConfigurationId} has overdue execution (scheduled: {ScheduledTime}, current: {CurrentTime})",
                            configuration.Id,
                            nextExecution.Value,
                            checkTime);
                    }
                }
                catch (Exception ex)
                {
                    // T104: Continue processing other configurations
                    _logger.LogError(
                        ex,
                        "Error evaluating configuration {ConfigurationId}: {ErrorMessage}",
                        configuration.Id,
                        ex.Message);
                }
            }

            // Wait for all triggered checks to be queued (not completed)
            if (tasks.Any())
            {
                await Task.WhenAll(tasks);
            }

            // T105: Structured logging with multi-configuration context
            _logger.LogInformation(
                "Scheduler check completed: {TriggeredCount} triggered, {SkippedCount} skipped (in-progress), {WaitingCount} deferred (concurrency limit), {ActiveCount} active configurations",
                triggeredCount,
                skippedCount,
                waitingCount,
                allConfigurations.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CheckScheduledConfigurationsAsync: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Triggers a file check for a specific configuration.
    /// </summary>
    private async Task TriggerFileCheckAsync(
        Domain.Entities.FileRetrievalConfiguration configuration,
        DateTimeOffset scheduledTime,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Triggering file check for configuration {ConfigurationId} (Client: {ClientId}, Name: '{Name}', Protocol: {Protocol})",
            configuration.Id,
            configuration.ClientId,
            configuration.Name,
            configuration.Protocol);

        // Send ExecuteFileCheck command
        var command = new ExecuteFileCheck
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = $"scheduled-{configuration.ClientId}-{configuration.Id}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            OccurredUtc = DateTimeOffset.UtcNow,
            IdempotencyKey = $"{configuration.ClientId}:{configuration.Id}:scheduled:{scheduledTime.Ticks}",
            ClientId = configuration.ClientId,
            ConfigurationId = configuration.Id,
            ScheduledExecutionTime = scheduledTime
        };

        var sendOptions = new SendOptions();
        sendOptions.SetDestination(ExecuteFileCheckDestination);

        await _messageSession.Send(command, sendOptions, cancellationToken);

        _logger.LogDebug(
            "ExecuteFileCheck command sent for configuration {ConfigurationId} to endpoint {Destination}",
            configuration.Id,
            ExecuteFileCheckDestination);
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SchedulerHostedService is starting");
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SchedulerHostedService is stopping");
        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _concurrencyLimiter?.Dispose();
        base.Dispose();
    }
}
