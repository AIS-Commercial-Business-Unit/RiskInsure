using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiskInsure.FileRetrieval.Domain.ValueObjects;
using RiskInsure.FileRetrieval.Infrastructure.Scheduling;
using Xunit;

namespace RiskInsure.FileRetrieval.Infrastructure.Tests.Scheduling;

/// <summary>
/// Additional tests for ScheduleEvaluator advanced scenarios
/// </summary>
public class ScheduleEvaluatorAdvancedTests
{
    private readonly ScheduleEvaluator _evaluator;
    private readonly Mock<ILogger<ScheduleEvaluator>> _loggerMock;

    public ScheduleEvaluatorAdvancedTests()
    {
        _loggerMock = new Mock<ILogger<ScheduleEvaluator>>();
        _evaluator = new ScheduleEvaluator(_loggerMock.Object);
    }

    [Fact]
    public void GetNextExecutionTime_WithEveryMinuteCron_ShouldBeWithinOneMinute()
    {
        // Arrange
        var schedule = new ScheduleDefinition(
            cronExpression: "* * * * *",  // Every minute
            timezone: "UTC"
        );
        var fromTime = DateTimeOffset.UtcNow;

        // Act
        var nextExecution = _evaluator.GetNextExecutionTime(schedule, fromTime);

        // Assert
        nextExecution.Should().NotBeNull();
        var timeDiff = nextExecution!.Value - fromTime;
        timeDiff.TotalMinutes.Should().BeLessThanOrEqualTo(1);
        timeDiff.TotalSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetNextExecutionTime_WithSpecificDayCron_ShouldRespectDayOfMonth()
    {
        // Arrange
        var schedule = new ScheduleDefinition(
            cronExpression: "0 12 15 * *",  // 15th of every month at noon
            timezone: "UTC"
        );
        var fromTime = new DateTimeOffset(2026, 2, 10, 10, 0, 0, TimeSpan.Zero);

        // Act
        var nextExecution = _evaluator.GetNextExecutionTime(schedule, fromTime);

        // Assert
        nextExecution.Should().NotBeNull();
        nextExecution!.Value.Day.Should().Be(15);
        nextExecution.Value.Month.Should().Be(2); // Same month
        nextExecution.Value.Hour.Should().Be(12);
    }

    [Fact]
    public void GetNextExecutionTime_WithWeekdaysCron_ShouldSkipWeekends()
    {
        // Arrange
        var schedule = new ScheduleDefinition(
            cronExpression: "0 9 * * 1-5",  // Weekdays at 9 AM
            timezone: "UTC"
        );
        // Start on Saturday
        var fromTime = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero); // Saturday

        // Act
        var nextExecution = _evaluator.GetNextExecutionTime(schedule, fromTime);

        // Assert
        nextExecution.Should().NotBeNull();
        // Should be Monday (2/23/2026 is Monday)
        nextExecution!.Value.DayOfWeek.Should().Be(DayOfWeek.Monday);
        nextExecution.Value.Hour.Should().Be(9);
    }

    [Fact]
    public void IsValidCronExpression_WithValidExpressions_ShouldReturnTrue()
    {
        // Arrange & Act & Assert
        _evaluator.IsValidCronExpression("0 9 * * *").Should().BeTrue();
        _evaluator.IsValidCronExpression("*/5 * * * *").Should().BeTrue();
        _evaluator.IsValidCronExpression("0 0 * * 0").Should().BeTrue(); // Sundays at midnight
        _evaluator.IsValidCronExpression("0 */4 * * *").Should().BeTrue(); // Every 4 hours
    }

    [Fact]
    public void IsValidCronExpression_WithInvalidExpressions_ShouldReturnFalse()
    {
        // Arrange & Act & Assert
        _evaluator.IsValidCronExpression("invalid").Should().BeFalse();
        _evaluator.IsValidCronExpression("").Should().BeFalse();
        _evaluator.IsValidCronExpression("* * * *").Should().BeFalse(); // Only 4 fields
        _evaluator.IsValidCronExpression("60 * * * *").Should().BeFalse(); // Invalid minute
    }

    [Fact]
    public void IsValidTimezone_WithValidTimezones_ShouldReturnTrue()
    {
        // Arrange & Act & Assert
        _evaluator.IsValidTimezone("UTC").Should().BeTrue();
        _evaluator.IsValidTimezone("America/New_York").Should().BeTrue();
        _evaluator.IsValidTimezone("Europe/London").Should().BeTrue();
        _evaluator.IsValidTimezone("Pacific Standard Time").Should().BeTrue(); // Windows format
    }

    [Fact]
    public void IsValidTimezone_WithInvalidTimezones_ShouldReturnFalse()
    {
        // Arrange & Act & Assert
        _evaluator.IsValidTimezone("Invalid/Timezone").Should().BeFalse();
        _evaluator.IsValidTimezone("").Should().BeFalse();
        _evaluator.IsValidTimezone("Not_A_Real_Timezone").Should().BeFalse();
    }

    [Fact]
    public void GetNextExecutionDescription_WithDaysAway_ShouldReturnDays()
    {
        // Arrange
        var schedule = new ScheduleDefinition(
            cronExpression: "0 0 1 * *",  // First of month
            timezone: "UTC"
        );
        var fromTime = new DateTimeOffset(2026, 2, 23, 10, 0, 0, TimeSpan.Zero);

        // Act
        var description = _evaluator.GetNextExecutionDescription(schedule, fromTime);

        // Assert
        description.Should().Contain("days");
    }

    [Fact]
    public void GetNextExecutionTime_WithEvery4HoursCron_ShouldAlignCorrectly()
    {
        // Arrange
        var schedule = new ScheduleDefinition(
            cronExpression: "0 */4 * * *",  // Every 4 hours (:00, :04, :08, :12, :16, :20)
            timezone: "UTC"
        );
        var fromTime = new DateTimeOffset(2026, 2, 23, 10, 30, 0, TimeSpan.Zero); // 10:30 AM

        // Act
        var nextExecution = _evaluator.GetNextExecutionTime(schedule, fromTime);

        // Assert
        nextExecution.Should().NotBeNull();
        nextExecution!.Value.Hour.Should().Be(12); // Next 4-hour mark
        nextExecution.Value.Minute.Should().Be(0);
        (nextExecution.Value.Hour % 4).Should().Be(0); // Divisible by 4
    }

    [Fact]
    public void GetNextExecutionTime_AfterScheduledTime_ShouldCalculateNextOccurrence()
    {
        // Arrange
        var schedule = new ScheduleDefinition(
            cronExpression: "0 9 * * *",  // Daily at 9 AM
            timezone: "UTC"
        );
        var fromTime = new DateTimeOffset(2026, 2, 23, 10, 0, 0, TimeSpan.Zero); // 10 AM (after 9 AM)

        // Act
        var nextExecution = _evaluator.GetNextExecutionTime(schedule, fromTime);

        // Assert
        nextExecution.Should().NotBeNull();
        nextExecution!.Value.Day.Should().Be(24); // Next day
        nextExecution.Value.Hour.Should().Be(9);
    }
}
