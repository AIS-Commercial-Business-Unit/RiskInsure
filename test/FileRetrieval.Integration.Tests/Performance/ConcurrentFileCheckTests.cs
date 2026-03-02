using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using RiskInsure.FileRetrieval.Application.Services;
using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.ValueObjects;
using RiskInsure.FileRetrieval.Domain.Repositories;
using RiskInsure.FileRetrieval.Application.Protocols;
using Moq;
using Microsoft.Extensions.Logging;
using NServiceBus;

namespace RiskInsure.FileRetrieval.Tests.Performance;

/// <summary>
/// T137: Performance tests for 100+ concurrent file checks (SC-004 validation).
/// Target: Support 100 concurrent file checks without performance degradation.
/// </summary>
public class ConcurrentFileCheckTests
{
    private readonly ITestOutputHelper _output;

    public ConcurrentFileCheckTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ExecuteFileCheck_With100ConcurrentChecks_CompletesWithin30Seconds()
    {
        // Arrange
        var mockConfigRepo = new Mock<IFileRetrievalConfigurationRepository>();
        var mockExecutionRepo = new Mock<IFileRetrievalExecutionRepository>();
        var mockDiscoveredFileRepo = new Mock<IDiscoveredFileRepository>();
        var mockLogger = new Mock<ILogger<FileCheckService>>();

        var configurations = Enumerable.Range(1, 100)
            .Select(i => CreateTestConfiguration($"client-test", $"config-{i}"))
            .ToList();

        foreach (var config in configurations)
        {
            mockConfigRepo.Setup(r => r.GetByIdAsync(config.Id, config.ClientId))
                .ReturnsAsync(config);
        }

        mockExecutionRepo.Setup(r => r.CreateAsync(It.IsAny<FileRetrievalExecution>()))
            .ReturnsAsync((FileRetrievalExecution exec) => exec);

        mockExecutionRepo.Setup(r => r.UpdateAsync(It.IsAny<FileRetrievalExecution>()))
            .ReturnsAsync((FileRetrievalExecution exec) => exec);

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

        var fileCheckService = new FileCheckService(
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
            fileCheckService.ExecuteCheckAsync(config.Id, config.ClientId, CancellationToken.None)
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
    public async Task ExecuteFileCheck_With100ConcurrentChecks_MaintainsThroughput()
    {
        // Arrange
        var mockConfigRepo = new Mock<IFileRetrievalConfigurationRepository>();
        var mockExecutionRepo = new Mock<IFileRetrievalExecutionRepository>();
        var mockDiscoveredFileRepo = new Mock<IDiscoveredFileRepository>();
        var mockLogger = new Mock<ILogger<FileCheckService>>();
        var mockMessageSession = new Mock<IMessageSession>();

        var configurations = Enumerable.Range(1, 100)
            .Select(i => CreateTestConfiguration($"client-test", $"config-{i}"))
            .ToList();

        foreach (var config in configurations)
        {
            mockConfigRepo.Setup(r => r.GetByIdAsync(config.Id, config.ClientId))
                .ReturnsAsync(config);
        }

        mockExecutionRepo.Setup(r => r.CreateAsync(It.IsAny<FileRetrievalExecution>()))
            .ReturnsAsync((FileRetrievalExecution exec) => exec);

        mockExecutionRepo.Setup(r => r.UpdateAsync(It.IsAny<FileRetrievalExecution>()))
            .ReturnsAsync((FileRetrievalExecution exec) => exec);

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

        var fileCheckService = new FileCheckService(
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
            fileCheckService.ExecuteCheckAsync(config.Id, config.ClientId, CancellationToken.None)
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

    private static FileRetrievalConfiguration CreateTestConfiguration(string clientId, string name)
    {
        return new FileRetrievalConfiguration
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
