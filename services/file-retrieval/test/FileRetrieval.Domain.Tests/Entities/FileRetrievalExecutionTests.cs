using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Enums;
using FluentAssertions;

namespace FileRetrieval.Domain.Tests.Entities;

public class FileRetrievalExecutionTests
{
    [Fact]
    public void Validate_WithValidExecution_ShouldNotThrow()
    {
        // Arrange
        var execution = CreateValidExecution();

        // Act
        var act = () => execution.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithNegativeFilesFound_ShouldThrowArgumentException()
    {
        // Arrange
        var execution = CreateValidExecution();
        execution.FilesFound = -1;

        // Act
        var act = () => execution.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*FilesFound*negative*");
    }

    [Fact]
    public void Validate_WithFilesProcessedGreaterThanFound_ShouldThrowArgumentException()
    {
        // Arrange
        var execution = CreateValidExecution();
        execution.FilesFound = 5;
        execution.FilesProcessed = 10;

        // Act
        var act = () => execution.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*FilesProcessed*between*FilesFound*");
    }

    private static FileRetrievalExecution CreateValidExecution()
    {
        return new FileRetrievalExecution
        {
            Id = Guid.NewGuid(),
            ClientId = "client123",
            ConfigurationId = Guid.NewGuid(),
            ExecutionStartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExecutionCompletedAt = DateTimeOffset.UtcNow,
            Status = ExecutionStatus.Completed,
            FilesFound = 3,
            FilesProcessed = 3,
            ResolvedFilePathPattern = "/files/2026/02",
            ResolvedFilenamePattern = "data-2026-02-23",
            DurationMs = 5000,
            RetryCount = 0,
            ETag = "\"00000000-0000-0000-0000-000000000000\""
        };
    }
}
