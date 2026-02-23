using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.ValueObjects;
using FluentAssertions;
using FtpSettings = RiskInsure.FileRetrieval.Domain.ValueObjects.FtpProtocolSettings;

namespace FileRetrieval.Domain.Tests.Entities;

public class FileRetrievalConfigurationTests
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

    private static FileRetrievalConfiguration CreateValidConfiguration()
    {
        var ftpSettings = new FtpProtocolSettings(
            "ftp.example.com",
            21,
            "testuser",
            "testpass-secret",
            useTls: true,
            usePassiveMode: true
        );

        return new FileRetrievalConfiguration
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
            EventsToPublish = new List<EventDefinition>
            {
                new EventDefinition("FileDiscovered", new Dictionary<string, object>())
            },
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test-user",
            ETag = "etag123"
        };
    }
}
