namespace RiskInsure.FileRetrieval.Domain.ValueObjects;

/// <summary>
/// T027: Schedule definition for file retrieval configuration.
/// Defines when a FileRetrievalConfiguration should execute using cron expressions.
/// </summary>
public sealed class ScheduleDefinition
{
    /// <summary>
    /// Cron expression (standard 5-field format: minute hour day month dayOfWeek)
    /// </summary>
    public string CronExpression { get; init; }

    /// <summary>
    /// IANA timezone (e.g., "America/New_York", "UTC")
    /// </summary>
    public string Timezone { get; init; }

    /// <summary>
    /// Human-readable schedule description (optional)
    /// </summary>
    public string? Description { get; init; }

    public ScheduleDefinition(string cronExpression, string timezone, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentException("CronExpression cannot be empty", nameof(cronExpression));
        if (string.IsNullOrWhiteSpace(timezone))
            throw new ArgumentException("Timezone cannot be empty", nameof(timezone));
        if (description?.Length > 200)
            throw new ArgumentException("Description cannot exceed 200 characters", nameof(description));

        // Validate timezone
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException($"Invalid timezone: {timezone}", nameof(timezone));
        }

        CronExpression = cronExpression;
        Timezone = timezone;
        Description = description;
    }
}
