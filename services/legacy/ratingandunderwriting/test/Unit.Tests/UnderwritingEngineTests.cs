namespace RiskInsure.RatingAndUnderwriting.Tests.Unit;

using FluentAssertions;
using RiskInsure.RatingAndUnderwriting.Domain.Services;
using Xunit;

public class UnderwritingEngineTests
{
    private readonly UnderwritingEngine _engine = new();

    [Fact]
    public void Evaluate_ClassA_ReturnsApproved()
    {
        // Arrange
        var submission = new UnderwritingSubmission(0, 10, "Excellent");

        // Act
        var result = _engine.Evaluate(submission);

        // Assert
        result.IsApproved.Should().BeTrue();
        result.UnderwritingClass.Should().Be("A");
        result.DeclineReason.Should().BeNull();
    }

    [Fact]
    public void Evaluate_ClassB_ReturnsApproved()
    {
        // Arrange
        var submission = new UnderwritingSubmission(1, 25, "Good");

        // Act
        var result = _engine.Evaluate(submission);

        // Assert
        result.IsApproved.Should().BeTrue();
        result.UnderwritingClass.Should().Be("B");
        result.DeclineReason.Should().BeNull();
    }

    [Fact]
    public void Evaluate_ExcessiveClaims_ReturnsDeclined()
    {
        // Arrange
        var submission = new UnderwritingSubmission(3, 20, "Good");

        // Act
        var result = _engine.Evaluate(submission);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.UnderwritingClass.Should().BeNull();
        result.DeclineReason.Should().Contain("prior claims");
    }

    [Fact]
    public void Evaluate_AgeExceeded_ReturnsDeclined()
    {
        // Arrange
        var submission = new UnderwritingSubmission(0, 35, "Excellent");

        // Act
        var result = _engine.Evaluate(submission);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.UnderwritingClass.Should().BeNull();
        result.DeclineReason.Should().Contain("age exceeds");
    }

    [Fact]
    public void Evaluate_PoorCredit_ReturnsDeclined()
    {
        // Arrange
        var submission = new UnderwritingSubmission(0, 10, "Poor");

        // Act
        var result = _engine.Evaluate(submission);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.UnderwritingClass.Should().BeNull();
        result.DeclineReason.Should().Contain("Credit tier");
    }
}
