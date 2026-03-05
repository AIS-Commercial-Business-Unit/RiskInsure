namespace RiskInsure.Customer.Unit.Tests;

using FluentAssertions;
using RiskInsure.Customer.Domain.Validation;
using Xunit;

public class CustomerValidatorTests
{
    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("user.name+tag@example.co.uk", true)]
    [InlineData("invalid", false)]
    [InlineData("@example.com", false)]
    [InlineData("test@", false)]
    [InlineData("", false)]
    public void IsValidEmail_ValidatesEmailFormat(string email, bool expected)
    {
        // Arrange
        var validator = new CustomerValidator(null!);

        // Act
        var result = validator.IsValidEmail(email);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("12345", true)]
    [InlineData("90210", true)]
    [InlineData("1234", false)]
    [InlineData("123456", false)]
    [InlineData("abcde", false)]
    [InlineData("", false)]
    public void IsValidZipCode_ValidatesZipCodeFormat(string zipCode, bool expected)
    {
        // Arrange
        var validator = new CustomerValidator(null!);

        // Act
        var result = validator.IsValidZipCode(zipCode);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsAgeEligible_ReturnsTrueForAge18AndAbove()
    {
        // Arrange
        var validator = new CustomerValidator(null!);
        var birthDate = DateTimeOffset.UtcNow.AddYears(-18);

        // Act
        var result = validator.IsAgeEligible(birthDate);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAgeEligible_ReturnsFalseForAgeBelow18()
    {
        // Arrange
        var validator = new CustomerValidator(null!);
        var birthDate = DateTimeOffset.UtcNow.AddYears(-17);

        // Act
        var result = validator.IsAgeEligible(birthDate);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAgeEligible_ReturnsTrueFor30YearOld()
    {
        // Arrange
        var validator = new CustomerValidator(null!);
        var birthDate = DateTimeOffset.UtcNow.AddYears(-30);

        // Act
        var result = validator.IsAgeEligible(birthDate);

        // Assert
        result.Should().BeTrue();
    }
}
