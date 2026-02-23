using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.FileRetrieval.Domain.Repositories;
using FileRetrieval.Contracts.Commands;
using System.Collections.Concurrent;
using System.Threading;

namespace RiskInsure.FileRetrieval.Infrastructure.Scheduling;

/// <summary>
/// Background service that periodically checks for scheduled file retrieval configurations
/// and triggers ExecuteFileCheck commands when schedules are due.
/// Runs every minute to check for scheduled executions.
/// </summary>
public class SchedulerHostedService : BackgroundService
{
    private readonly IFileRetrievalConfigurationRepository _configurationRepository;
    private readonly ScheduleEvaluator _scheduleEvaluator;
    private readonly IMessageSession _messageSession;
    private readonly ILogger<SchedulerHostedService> _logger;
    
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ExecutionWindow = TimeSpan.FromMinutes(2); // Allow 2-minute window for "due" checks
    private static readonly int MaxConcurrentChecks = 100; // T100: SC-004 requirement
    
    // T100: Semaphore to limit concurrent file checks (max 100 per SC-004)
    private readonly SemaphoreSlim _concurrencyLimiter;
    
    // T101: Track in-progress configurations to prevent duplicates within this instance
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _inProgressChecks;

    public SchedulerHostedService(
        IFileRetrievalConfigurationRepository configurationRepository,
        ScheduleEvaluator scheduleEvaluator,
        IMessageSession messageSession,
        ILogger<SchedulerHostedService> logger)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _scheduleEvaluator = scheduleEvaluator ?? throw new ArgumentNullException(nameof(scheduleEvaluator));
        _messageSession = messageSession ?? throw new ArgumentNullException(nameof(messageSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Initialize concurrency control (T100)
        _concurrencyLimiter = new SemaphoreSlim(MaxConcurrentChecks, MaxConcurrentChecks);
        _inProgressChecks = new ConcurrentDictionary<Guid, DateTimeOffset>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SchedulerHostedService starting - will check for scheduled executions every {Interval}", CheckInterval);

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

            // Wait for next check interval
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
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
            
            var allConfigurations = await _configurationRepository.GetAllActiveConfigurationsAsync(cancellationToken);

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
                    
                    if (timeDifference.TotalMinutes <= ExecutionWindow.TotalMinutes && timeDifference.TotalSeconds >= 0)
                    {
                        // T100: Wait for available slot (non-blocking)
                        if (_concurrencyLimiter.CurrentCount == 0)
                        {
                            waitingCount++;
                            _logger.LogWarning(
                                "Concurrent execution limit reached ({MaxConcurrent}). Configuration {ConfigurationId} will be deferred.",
                                MaxConcurrentChecks,
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

        await _messageSession.Send(command, cancellationToken);

        _logger.LogDebug(
            "ExecuteFileCheck command sent for configuration {ConfigurationId}",
            configuration.Id);
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
