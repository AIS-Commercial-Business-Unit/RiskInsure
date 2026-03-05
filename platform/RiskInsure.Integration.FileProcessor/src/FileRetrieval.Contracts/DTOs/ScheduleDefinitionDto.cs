namespace FileRetrieval.Contracts.DTOs;

/// <summary>
/// Schedule definition for when to execute file checks.
/// Uses cron expressions for flexible scheduling.
/// </summary>
public record ScheduleDefinitionDto
{
    /// <summary>
    /// Cron expression defining the schedule (e.g., "0 8 * * *" for daily at 8 AM).
    /// Format: minute hour day-of-month month day-of-week
    /// </summary>
    public required string CronExpression { get; init; }

    /// <summary>
    /// Timezone for cron expression evaluation (e.g., "America/New_York", "UTC").
    /// Must be a valid IANA timezone identifier.
    /// </summary>
    public required string Timezone { get; init; }

    /// <summary>
    /// Optional human-readable description of the schedule (e.g., "Daily at 8 AM EST").
    /// </summary>
    public string? Description { get; init; }
}
