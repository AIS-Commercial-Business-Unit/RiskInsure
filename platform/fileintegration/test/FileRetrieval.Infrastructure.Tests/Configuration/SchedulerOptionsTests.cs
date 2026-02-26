using FluentAssertions;
using RiskInsure.FileRetrieval.Infrastructure.Configuration;
using Xunit;

namespace RiskInsure.FileRetrieval.Infrastructure.Tests.Configuration;

/// <summary>
/// Tests for SchedulerOptions configuration class
/// </summary>
public class SchedulerOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var options = new SchedulerOptions();

        // Assert
        options.PollingIntervalSeconds.Should().Be(60);
        options.MaxConcurrentChecks.Should().Be(100);
        options.ExecutionWindowMinutes.Should().Be(2);
        options.EnableDistributedLocking.Should().BeTrue();
        
        // Should not throw
        var validationAction = () => options.Validate();
        validationAction.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithValidSettings_ShouldNotThrow()
    {
        // Arrange
        var options = new SchedulerOptions
        {
            PollingIntervalSeconds = 30,
            MaxConcurrentChecks = 50,
            ExecutionWindowMinutes = 5,
            EnableDistributedLocking = false
        };

        // Act & Assert
        var validationAction = () => options.Validate();
        validationAction.Should().NotThrow();
    }

    [Theory]
    [InlineData(0, "PollingIntervalSeconds must be at least 1 second")]
    [InlineData(-1, "PollingIntervalSeconds must be at least 1 second")]
    [InlineData(3601, "PollingIntervalSeconds must not exceed 3600 seconds")]
    public void Validate_WithInvalidPollingInterval_ShouldThrow(int interval, string expectedMessage)
    {
        // Arrange
        var options = new SchedulerOptions
        {
            PollingIntervalSeconds = interval
        };

        // Act & Assert
        var validationAction = () => options.Validate();
        validationAction.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{expectedMessage}*");
    }

    [Theory]
    [InlineData(0, "MaxConcurrentChecks must be at least 1")]
    [InlineData(-5, "MaxConcurrentChecks must be at least 1")]
    [InlineData(1001, "MaxConcurrentChecks must not exceed 1000")]
    public void Validate_WithInvalidMaxConcurrentChecks_ShouldThrow(int maxChecks, string expectedMessage)
    {
        // Arrange
        var options = new SchedulerOptions
        {
            MaxConcurrentChecks = maxChecks
        };

        // Act & Assert
        var validationAction = () => options.Validate();
        validationAction.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{expectedMessage}*");
    }

    [Theory]
    [InlineData(0, "ExecutionWindowMinutes must be at least 1 minute")]
    [InlineData(-1, "ExecutionWindowMinutes must be at least 1 minute")]
    [InlineData(61, "ExecutionWindowMinutes must not exceed 60 minutes")]
    public void Validate_WithInvalidExecutionWindow_ShouldThrow(int window, string expectedMessage)
    {
        // Arrange
        var options = new SchedulerOptions
        {
            ExecutionWindowMinutes = window
        };

        // Act & Assert
        var validationAction = () => options.Validate();
        validationAction.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{expectedMessage}*");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(60, 60)]
    [InlineData(300, 300)]
    [InlineData(3600, 3600)]
    public void GetPollingInterval_ShouldReturnCorrectTimeSpan(int seconds, int expectedSeconds)
    {
        // Arrange
        var options = new SchedulerOptions
        {
            PollingIntervalSeconds = seconds
        };

        // Act
        var interval = options.GetPollingInterval();

        // Assert
        interval.TotalSeconds.Should().Be(expectedSeconds);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(5, 5)]
    [InlineData(60, 60)]
    public void GetExecutionWindow_ShouldReturnCorrectTimeSpan(int minutes, int expectedMinutes)
    {
        // Arrange
        var options = new SchedulerOptions
        {
            ExecutionWindowMinutes = minutes
        };

        // Act
        var window = options.GetExecutionWindow();

        // Assert
        window.TotalMinutes.Should().Be(expectedMinutes);
    }

    [Fact]
    public void SectionName_ShouldBeScheduler()
    {
        // Assert
        SchedulerOptions.SectionName.Should().Be("Scheduler");
    }

    [Fact]
    public void Validate_WithMinimumValidValues_ShouldNotThrow()
    {
        // Arrange
        var options = new SchedulerOptions
        {
            PollingIntervalSeconds = 1,
            MaxConcurrentChecks = 1,
            ExecutionWindowMinutes = 1
        };

        // Act & Assert
        var validationAction = () => options.Validate();
        validationAction.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithMaximumValidValues_ShouldNotThrow()
    {
        // Arrange
        var options = new SchedulerOptions
        {
            PollingIntervalSeconds = 3600,
            MaxConcurrentChecks = 1000,
            ExecutionWindowMinutes = 60
        };

        // Act & Assert
        var validationAction = () => options.Validate();
        validationAction.Should().NotThrow();
    }
}
