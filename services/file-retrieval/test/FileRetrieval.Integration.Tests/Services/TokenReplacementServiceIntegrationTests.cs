using FluentAssertions;
using RiskInsure.FileRetrieval.Application.Services;

namespace FileRetrieval.Integration.Tests.Services;

public class TokenReplacementServiceIntegrationTests
{
    private readonly TokenReplacementService _service;

    public TokenReplacementServiceIntegrationTests()
    {
        _service = new TokenReplacementService();
    }

    [Theory]
    [InlineData("/files/{yyyy}/{mm}/{dd}", 2026, 2, 23, "/files/2026/02/23")]
    [InlineData("/archive-{yyyy}-{mm}-{dd}", 2025, 12, 31, "/archive-2025-12-31")]
    [InlineData("/reports/{yyyy}/Q1", 2024, 3, 15, "/reports/2024/Q1")]
    public void ReplaceTokens_WithDateTokens_ReplacesCorrectly(
        string pattern,
        int year,
        int month,
        int day,
        string expected)
    {
        // Arrange
        var date = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = _service.ReplaceTokens(pattern, date);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("{yyyy}-{mm}-{dd}", "2026-02-23")]
    [InlineData("{yy}/{mm}/{dd}", "26/02/23")]
    [InlineData("report-{yyyy}{mm}{dd}", "report-20260223")]
    public void ReplaceTokens_WithCurrentDate_ReplacesWithTodayValues(
        string pattern,
        string expectedFormat)
    {
        // Arrange
        var today = new DateTimeOffset(2026, 2, 23, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = _service.ReplaceTokens(pattern, today);

        // Assert
        result.Should().Be(expectedFormat);
    }

    [Fact]
    public void ReplaceTokens_WithNoTokens_ReturnsOriginalString()
    {
        // Arrange
        var pattern = "/files/data/report.csv";
        var date = DateTimeOffset.UtcNow;

        // Act
        var result = _service.ReplaceTokens(pattern, date);

        // Assert
        result.Should().Be(pattern);
    }

    [Theory]
    [InlineData("{yyyy}-{mm}-{dd}", true)]
    [InlineData("/files/static/data.csv", false)]
    [InlineData("{yyyy}", true)]
    [InlineData("{invalid}", false)]
    public void ContainsTokens_DetectsTokensCorrectly(string pattern, bool expected)
    {
        // Act
        var result = _service.ContainsTokens(pattern);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("{yyyy}-{mm}-{dd}", true)]
    [InlineData("{yyyy}/{mm}/{dd}", true)]
    public void ValidatePatterns_ChecksPatternValidity(string pattern, bool shouldBeValid)
    {
        // Act
        var result = _service.ValidatePatterns("server", pattern, "test");

        // Assert
        if (shouldBeValid)
        {
            result.IsValid.Should().BeTrue();
        }
        else
        {
            result.IsValid.Should().BeFalse();
        }
    }

    [Fact]
    public void ReplaceTokens_WithLeapYear_HandlesCorrectly()
    {
        // Arrange
        var pattern = "{yyyy}-{mm}-{dd}";
        var leapDay = new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = _service.ReplaceTokens(pattern, leapDay);

        // Assert
        result.Should().Be("2024-02-29");
    }

    [Fact]
    public void ReplaceTokens_WithYearEnd_HandlesCorrectly()
    {
        // Arrange
        var pattern = "{yyyy}/{mm}/{dd}";
        var yearEnd = new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = _service.ReplaceTokens(pattern, yearEnd);

        // Assert
        result.Should().Be("2025/12/31");
    }
}
