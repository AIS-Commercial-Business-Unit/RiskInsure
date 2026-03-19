using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using RiskInsure.FileProcessing.Application.Services;
using RiskInsure.FileProcessing.Domain.Entities;
using RiskInsure.FileProcessing.Domain.Enums;
using RiskInsure.FileProcessing.Domain.ValueObjects;
using RiskInsure.FileProcessing.Domain.Repositories;
using RiskInsure.FileProcessing.Application.Protocols;
using Moq;
using Microsoft.Extensions.Logging;
using NServiceBus;

namespace RiskInsure.FileProcessing.Tests.Performance;

/// <summary>
/// T137: Performance tests for 100+ concurrent file checks (SC-004 validation).
/// Target: Support 100 concurrent file checks without performance degradation.
/// </summary>
public class ConcurrentRetrieveFileTests
{
    private readonly ITestOutputHelper _output;

    public ConcurrentRetrieveFileTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task RetrieveFile_With100ConcurrentChecks_CompletesWithin30Seconds()
    {
        // Arrange
        var mockConfigRepo = new Mock<IFileProcessingConfigurationRepository>();
        var mockExecutionRepo = new Mock<IFileProcessingExecutionRepository>();
        var mockDiscoveredFileRepo = new Mock<IDiscoveredFileRepository>();
        var mockLogger = new Mock<ILogger<RetrieveFileService>>();

        var configurations = Enumerable.Range(1, 100)
            .Select(i => CreateTestConfiguration($"client-test", $"config-{i}"))
            .ToList();

        foreach (var config in configurations)
        {
            mockConfigRepo.Setup(r => r.GetByIdAsync(config.Id, config.ClientId))
                .ReturnsAsync(config);
        }

        mockExecutionRepo.Setup(r => r.CreateAsync(It.IsAny<FileProcessingExecution>()))
            .ReturnsAsync((FileProcessingExecution exec) => exec);

        mockExecutionRepo.Setup(r => r.UpdateAsync(It.IsAny<FileProcessingExecution>()))
            .ReturnsAsync((FileProcessingExecution exec) => exec);

        // Mock protocol adapter that simulates 100ms network delay
        var mockProtocolAdapter = new Mock<IProtocolAdapter>();
        mockProtocolAdapter.Setup(a => a.CheckForFilesAsync(
                It.IsAny<ProtocolSettings>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (ProtocolSettings settings, string path, string filename, string? extension, CancellationToken ct) =>
            {
                await Task.Delay(100, ct); // Simulate network delay
                return new List<DiscoveredFileInfo>
                {
                    new DiscoveredFileInfo(
                        $"https://test.com/file-{Guid.NewGuid()}.csv",
                        $"file-{Guid.NewGuid()}.csv",
                        1024,
                        DateTimeOffset.UtcNow)
                };
            });

        var mockProtocolFactory = new Mock<IProtocolAdapterFactory>();
        mockProtocolFactory.Setup(f => f.GetAdapter(It.IsAny<ProtocolType>()))
            .Returns(mockProtocolAdapter.Object);

        var tokenService = new TokenReplacementService(Mock.Of<ILogger<TokenReplacementService>>());
        var mockMessageSession = new Mock<IMessageSession>();

        var RetrieveFileService = new RetrieveFileService(
            mockConfigRepo.Object,
            mockExecutionRepo.Object,
            mockDiscoveredFileRepo.Object,
            mockProtocolFactory.Object,
            tokenService,
            mockMessageSession.Object,
            mockLogger.Object);

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        var tasks = configurations.Select(config => 
            RetrieveFileService.ExecuteCheckAsync(config.Id, config.ClientId, CancellationToken.None)
        ).ToList();

        await Task.WhenAll(tasks);
        
        stopwatch.Stop();

        // Assert
        var durationSeconds = stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"100 concurrent checks completed in {durationSeconds:F2} seconds");
        _output.WriteLine($"Average duration per check: {(durationSeconds / 100):F2} seconds");

        // Target: Complete within 30 seconds (with 100ms mock delay = ~10s minimum + overhead)
        Assert.True(durationSeconds < 30, 
            $"Performance degradation detected: {durationSeconds:F2}s > 30s target");

        // Verify all checks completed
        Assert.All(tasks, task => Assert.True(task.IsCompletedSuccessfully));
    }

    [Fact]
    public async Task RetrieveFile_With100ConcurrentChecks_MaintainsThroughput()
    {
        // Arrange
        var mockConfigRepo = new Mock<IFileProcessingConfigurationRepository>();
        var mockExecutionRepo = new Mock<IFileProcessingExecutionRepository>();
        var mockDiscoveredFileRepo = new Mock<IDiscoveredFileRepository>();
        var mockLogger = new Mock<ILogger<RetrieveFileService>>();
        var mockMessageSession = new Mock<IMessageSession>();

        var configurations = Enumerable.Range(1, 100)
            .Select(i => CreateTestConfiguration($"client-test", $"config-{i}"))
            .ToList();

        foreach (var config in configurations)
        {
            mockConfigRepo.Setup(r => r.GetByIdAsync(config.Id, config.ClientId))
                .ReturnsAsync(config);
        }

        mockExecutionRepo.Setup(r => r.CreateAsync(It.IsAny<FileProcessingExecution>()))
            .ReturnsAsync((FileProcessingExecution exec) => exec);

        mockExecutionRepo.Setup(r => r.UpdateAsync(It.IsAny<FileProcessingExecution>()))
            .ReturnsAsync((FileProcessingExecution exec) => exec);

        var mockProtocolAdapter = new Mock<IProtocolAdapter>();
        mockProtocolAdapter.Setup(a => a.CheckForFilesAsync(
                It.IsAny<ProtocolSettings>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (ProtocolSettings settings, string path, string filename, string? extension, CancellationToken ct) =>
            {
                await Task.Delay(50, ct); // Simulate faster network
                return new List<DiscoveredFileInfo>();
            });

        var mockProtocolFactory = new Mock<IProtocolAdapterFactory>();
        mockProtocolFactory.Setup(f => f.GetAdapter(It.IsAny<ProtocolType>()))
            .Returns(mockProtocolAdapter.Object);

        var tokenService = new TokenReplacementService(Mock.Of<ILogger<TokenReplacementService>>());

        var RetrieveFileService = new RetrieveFileService(
            mockConfigRepo.Object,
            mockExecutionRepo.Object,
            mockDiscoveredFileRepo.Object,
            mockProtocolFactory.Object,
            tokenService,
            mockMessageSession.Object,
            mockLogger.Object);

        // Act - measure throughput
        var stopwatch = Stopwatch.StartNew();
        
        var tasks = configurations.Select(config => 
            RetrieveFileService.ExecuteCheckAsync(config.Id, config.ClientId, CancellationToken.None)
        ).ToList();

        await Task.WhenAll(tasks);
        
        stopwatch.Stop();

        // Assert
        var throughput = concurrencyLevel / stopwatch.Elapsed.TotalSeconds; // checks per second
        _output.WriteLine($"Concurrency: {concurrencyLevel}, Throughput: {throughput:F2} checks/second");

        // Target: At least 5 checks per second regardless of concurrency level
        Assert.True(throughput >= 5.0, 
            $"Throughput below target at {concurrencyLevel} concurrency: {throughput:F2} checks/sec < 5 checks/sec");
    }

    private static FileProcessingConfiguration CreateTestConfiguration(string clientId, string name)
    {
        return new FileProcessingConfiguration
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = name,
            Description = "Test configuration",
            Protocol = ProtocolType.Https,
            ProtocolSettings = new HttpsProtocolSettings
            {
                BaseUrl = "https://test.example.com",
                AuthenticationType = AuthType.None,
                ConnectionTimeout = TimeSpan.FromSeconds(30),
                FollowRedirects = true,
                MaxRedirects = 3
            },
            FilePathPattern = "/files/{yyyy}/{mm}",
            FilenamePattern = "test_{yyyy}{mm}{dd}.csv",
            FileExtension = "csv",
            Schedule = new ScheduleDefinition
            {
                CronExpression = "0 8 * * *",
                Timezone = "UTC",
                Description = "Daily at 8 AM"
            },
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test-user",
            ETag = Guid.NewGuid().ToString()
        };
    }
}
