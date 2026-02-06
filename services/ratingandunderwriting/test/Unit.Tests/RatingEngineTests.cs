namespace RiskInsure.RatingAndUnderwriting.Tests.Unit;

using FluentAssertions;
using RiskInsure.RatingAndUnderwriting.Domain.Models;
using RiskInsure.RatingAndUnderwriting.Domain.Services;
using Xunit;

public class RatingEngineTests
{
    private readonly RatingEngine _engine = new();

    [Fact]
    public void CalculatePremium_MinimumCoverage_ReturnsCorrectPremium()
    {
        // Arrange
        var quote = new Quote
        {
            StructureCoverageLimit = 100000,
            ContentsCoverageLimit = 50000,
            TermMonths = 12,
            KwegiboAge = 10
        };

        // Act
        var premium = _engine.CalculatePremium(quote, "60601");

        // Assert (BASE_RATE * 2.0 * 1.0 * 1.0 * 1.0 = 1000)
        premium.Should().Be(1000.00m);
    }

    [Fact]
    public void GetTermFactor_SixMonths_ReturnsHalfPlusFive()
    {
        // Act
        var factor = _engine.GetTermFactor(6);

        // Assert
        factor.Should().Be(0.55m);
    }

    [Fact]
    public void GetTermFactor_TwelveMonths_ReturnsOne()
    {
        // Act
        var factor = _engine.GetTermFactor(12);

        // Assert
        factor.Should().Be(1.00m);
    }

    [Fact]
    public void GetAgeFactor_YoungKwegibo_ReturnsDiscountedRate()
    {
        // Act
        var factor = _engine.GetAgeFactor(3);

        // Assert
        factor.Should().Be(0.80m);
    }

    [Fact]
    public void GetAgeFactor_OldKwegibo_ReturnsIncreasedRate()
    {
        // Act
        var factor = _engine.GetAgeFactor(35);

        // Assert
        factor.Should().Be(1.50m);
    }

    [Fact]
    public void GetTerritoryFactor_Zone1_ReturnsDiscountedRate()
    {
        // Act
        var factor = _engine.GetTerritoryFactor("90210");

        // Assert
        factor.Should().Be(0.90m);
    }

    [Fact]
    public void GetTerritoryFactor_UnknownZip_ReturnsDefaultRate()
    {
        // Act
        var factor = _engine.GetTerritoryFactor("12345");

        // Assert
        factor.Should().Be(1.10m);
    }
}
