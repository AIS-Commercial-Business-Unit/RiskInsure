using RiskInsure.FileProcessing.Domain.Entities;
using RiskInsure.FileProcessing.Domain.Enums;
using RiskInsure.FileProcessing.Domain.ValueObjects;
using FluentAssertions;
using FtpSettings = RiskInsure.FileProcessing.Domain.ValueObjects.FtpProtocolSettings;

namespace FileProcessing.Domain.Tests.Entities;

public class FileProcessingConfigurationTests
{
    [Fact]
    public void Validate_WithValidConfiguration_ShouldNotThrow()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithEmptyClientId_ShouldThrowArgumentException()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.ClientId = string.Empty;

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ClientId*");
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Name = string.Empty;

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Name*");
    }

    private static FileProcessingConfiguration CreateValidConfiguration()
    {
        var ftpSettings = new FtpProtocolSettings
        {
            Server = "ftp.example.com",
            Port = 21,
            Username = "testuser",
            Password = "testpass-secret",
            UseTls = true,
            UsePassiveMode = true
        };

        return new FileProcessingConfiguration
        {
            Id = Guid.NewGuid(),
            ClientId = "client123",
            Name = "Test Configuration",
            Description = "Test description",
            Protocol = ProtocolType.FTP,
            ProtocolSettings = ftpSettings,
            FilePathPattern = "/files/{yyyy}/{mm}",
            FilenamePattern = "data-{yyyy}-{mm}-{dd}",
            FileExtension = "csv",
            Schedule = new ScheduleDefinition("0 0 * * *", "UTC"),
            ProcessingConfig = new FileProcessingDefinition
            {
                FileType = "NACHA"
            },
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test-user",
            ETag = "etag123"
        };
    }
}
