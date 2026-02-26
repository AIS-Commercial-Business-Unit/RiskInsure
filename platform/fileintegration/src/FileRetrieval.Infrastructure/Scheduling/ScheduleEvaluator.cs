using NCrontab;
using RiskInsure.FileRetrieval.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace RiskInsure.FileRetrieval.Infrastructure.Scheduling;

/// <summary>
/// Evaluates cron expressions to determine next execution times.
/// Uses NCrontab library for robust cron parsing and evaluation.
/// </summary>
public class ScheduleEvaluator
{
    private readonly ILogger<ScheduleEvaluator> _logger;

    public ScheduleEvaluator(ILogger<ScheduleEvaluator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Calculates the next execution time for a schedule definition.
    /// </summary>
    /// <param name="schedule">Schedule definition with cron expression and timezone</param>
    /// <param name="fromTime">Base time to calculate from (defaults to UtcNow)</param>
    /// <returns>Next execution time in UTC, or null if schedule cannot be evaluated</returns>
    public DateTimeOffset? GetNextExecutionTime(ScheduleDefinition schedule, DateTimeOffset? fromTime = null)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        try
        {
            var baseTime = fromTime ?? DateTimeOffset.UtcNow;

            // Parse cron expression using NCrontab
            var includeSeconds = IncludesSeconds(schedule.CronExpression);
            var cronSchedule = CrontabSchedule.Parse(schedule.CronExpression, new CrontabSchedule.ParseOptions
            {
                IncludingSeconds = includeSeconds
            });

            // Get timezone info
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.Timezone);

            // Convert base time to schedule's timezone
            var baseTimeInScheduleZone = TimeZoneInfo.ConvertTime(baseTime, timeZone);

            // Get next occurrence in schedule's timezone
            var nextOccurrence = cronSchedule.GetNextOccurrence(baseTimeInScheduleZone.DateTime);

            // Convert back to UTC
            var nextOccurrenceInScheduleZone = new DateTimeOffset(
                nextOccurrence,
                timeZone.GetUtcOffset(nextOccurrence));

            var nextOccurrenceUtc = TimeZoneInfo.ConvertTimeToUtc(nextOccurrenceInScheduleZone.DateTime, timeZone);

            _logger.LogDebug(
                "Calculated next execution: {NextTime} UTC for cron '{CronExpression}' in timezone '{Timezone}'",
                nextOccurrenceUtc,
                schedule.CronExpression,
                schedule.Timezone);

            return new DateTimeOffset(nextOccurrenceUtc, TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error calculating next execution time for cron '{CronExpression}': {ErrorMessage}",
                schedule.CronExpression,
                ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Validates a cron expression.
    /// </summary>
    /// <param name="cronExpression">Cron expression to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValidCronExpression(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return false;
        }

        try
        {
            var includeSeconds = IncludesSeconds(cronExpression);
            CrontabSchedule.Parse(cronExpression, new CrontabSchedule.ParseOptions
            {
                IncludingSeconds = includeSeconds
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IncludesSeconds(string cronExpression)
    {
        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length switch
        {
            5 => false,
            6 => true,
            _ => throw new ArgumentException("Cron expression must contain 5 or 6 fields", nameof(cronExpression))
        };
    }

    /// <summary>
    /// Validates a timezone identifier.
    /// </summary>
    /// <param name="timezoneId">IANA or Windows timezone identifier</param>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValidTimezone(string timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
        {
            return false;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets a human-readable description of when a cron expression will next execute.
    /// </summary>
    /// <param name="schedule">Schedule definition</param>
    /// <param name="fromTime">Base time to calculate from (defaults to UtcNow)</param>
    /// <returns>Human-readable description, or error message</returns>
    public string GetNextExecutionDescription(ScheduleDefinition schedule, DateTimeOffset? fromTime = null)
    {
        var nextTime = GetNextExecutionTime(schedule, fromTime);
        
        if (nextTime == null)
        {
            return "Unable to calculate next execution time";
        }

        var timeUntil = nextTime.Value - (fromTime ?? DateTimeOffset.UtcNow);
        
        if (timeUntil.TotalMinutes < 1)
        {
            return "In less than 1 minute";
        }
        else if (timeUntil.TotalHours < 1)
        {
            return $"In {(int)timeUntil.TotalMinutes} minutes";
        }
        else if (timeUntil.TotalDays < 1)
        {
            return $"In {(int)timeUntil.TotalHours} hours";
        }
        else
        {
            return $"In {(int)timeUntil.TotalDays} days";
        }
    }
}
