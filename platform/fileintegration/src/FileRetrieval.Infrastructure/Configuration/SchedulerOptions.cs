namespace RiskInsure.FileRetrieval.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the file retrieval scheduler.
/// Controls how often the scheduler checks for due configurations and concurrency limits.
/// </summary>
public class SchedulerOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Scheduler";

    /// <summary>
    /// How often the scheduler checks for configurations that are due to run (in seconds).
    /// Default: 60 seconds (1 minute)
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum number of concurrent file checks that can run simultaneously.
    /// Default: 100
    /// </summary>
    public int MaxConcurrentChecks { get; set; } = 100;

    /// <summary>
    /// Time window (in minutes) to consider a configuration "due" for execution.
    /// If a configuration's next scheduled time falls within this window, it will be triggered.
    /// Default: 2 minutes
    /// </summary>
    public int ExecutionWindowMinutes { get; set; } = 2;

    /// <summary>
    /// Whether to enable distributed locking to prevent multiple worker instances
    /// from processing the same configuration.
    /// Default: true (recommended for production)
    /// </summary>
    public bool EnableDistributedLocking { get; set; } = true;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (PollingIntervalSeconds < 1)
            throw new InvalidOperationException($"{nameof(PollingIntervalSeconds)} must be at least 1 second");

        if (PollingIntervalSeconds > 3600)
            throw new InvalidOperationException($"{nameof(PollingIntervalSeconds)} must not exceed 3600 seconds (1 hour)");

        if (MaxConcurrentChecks < 1)
            throw new InvalidOperationException($"{nameof(MaxConcurrentChecks)} must be at least 1");

        if (MaxConcurrentChecks > 1000)
            throw new InvalidOperationException($"{nameof(MaxConcurrentChecks)} must not exceed 1000");

        if (ExecutionWindowMinutes < 1)
            throw new InvalidOperationException($"{nameof(ExecutionWindowMinutes)} must be at least 1 minute");

        if (ExecutionWindowMinutes > 60)
            throw new InvalidOperationException($"{nameof(ExecutionWindowMinutes)} must not exceed 60 minutes");
    }

    /// <summary>
    /// Gets the polling interval as a TimeSpan.
    /// </summary>
    public TimeSpan GetPollingInterval() => TimeSpan.FromSeconds(PollingIntervalSeconds);

    /// <summary>
    /// Gets the execution window as a TimeSpan.
    /// </summary>
    public TimeSpan GetExecutionWindow() => TimeSpan.FromMinutes(ExecutionWindowMinutes);
}
