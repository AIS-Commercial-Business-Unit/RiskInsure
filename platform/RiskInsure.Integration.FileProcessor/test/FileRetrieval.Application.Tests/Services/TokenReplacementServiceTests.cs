using RiskInsure.FileRetrieval.Application.Services;
using FluentAssertions;

namespace FileRetrieval.Application.Tests.Services;

public class TokenReplacementServiceTests
{
    private readonly TokenReplacementService _service;

    public TokenReplacementServiceTests()
    {
        _service = new TokenReplacementService();
    }

    [Fact]
    public void ReplaceTokens_WithYearToken_ShouldReplaceCorrectly()
    {
        // Arrange
        var pattern = "/files/{yyyy}/data.csv";
        var date = new DateTimeOffset(2026, 2, 23, 10, 0, 0, TimeSpan.Zero);

        // Act
        var result = _service.ReplaceTokens(pattern, date);

        // Assert
        result.Should().Be("/files/2026/data.csv");
    }

    [Fact]
    public void ReplaceTokens_WithMultipleTokens_ShouldReplaceAll()
    {
        // Arrange
        var pattern = "data-{yyyy}-{MM}-{dd}.csv";
        var date = new DateTimeOffset(2026, 2, 23, 14, 30, 45, TimeSpan.Zero);

        // Act
        var result = _service.ReplaceTokens(pattern, date);

        // Assert
        result.Should().Be("data-2026-02-23.csv");
    }

    [Fact]
    public void ReplaceTokens_WithNoTokens_ShouldReturnOriginal()
    {
        // Arrange
        var pattern = "/files/static/data.csv";
        var date = DateTimeOffset.UtcNow;

        // Act
        var result = _service.ReplaceTokens(pattern, date);

        // Assert
        result.Should().Be(pattern);
    }
}
