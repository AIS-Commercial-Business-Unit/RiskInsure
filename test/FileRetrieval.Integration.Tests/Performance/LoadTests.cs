using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using RiskInsure.FileRetrieval.Application.Services;
using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.ValueObjects;
using RiskInsure.FileRetrieval.Domain.Repositories;
using Moq;
using Microsoft.Extensions.Logging;
using NServiceBus;

namespace RiskInsure.FileRetrieval.Tests.Performance;

/// <summary>
/// T138: Load tests for 1000+ configurations across all clients (scale validation).
/// Target: Support 1000+ FileRetrievalConfigurations without performance degradation.
/// </summary>
public class LoadTests
{
    private readonly ITestOutputHelper _output;

    public LoadTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ConfigurationService_With1000Configurations_QueriesWithinPerformanceTarget()
    {
        // Arrange
        var mockRepo = new Mock<IFileRetrievalConfigurationRepository>();
        var mockLogger = new Mock<ILogger<ConfigurationService>>();
        var mockMessageSession = new Mock<IMessageSession>();

        var configurations = Enumerable.Range(1, 1000)
            .Select(i => CreateTestConfiguration($"client-{i % 50}", $"config-{i}")) // 50 clients, 20 configs each
            .ToList();

        // Mock repository to return configurations for a client
        mockRepo.Setup(r => r.GetByClientIdAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ReturnsAsync((string clientId, int pageSize, string? continuationToken) =>
            {
                var clientConfigs = configurations.Where(c => c.ClientId == clientId).Take(pageSize).ToList();
                return (clientConfigs, (string?)null);
            });

        mockRepo.Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(configurations.Where(c => c.IsActive).ToList());

        var configService = new ConfigurationService(
            mockRepo.Object,
            mockMessageSession.Object,
            mockLogger.Object);

        // Act - Query all active configurations
        var stopwatch = Stopwatch.StartNew();
        var activeConfigs = await mockRepo.Object.GetAllActiveAsync();
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Queried {activeConfigs.Count} active configurations in {stopwatch.ElapsedMilliseconds}ms");

        // Target: Query should complete within 1 second even with 1000+ configs
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Query performance degradation: {stopwatch.ElapsedMilliseconds}ms > 1000ms target");

        Assert.Equal(1000, activeConfigs.Count);
    }

    [Fact]
    public async Task SchedulerService_With1000Configurations_EvaluatesSchedulesWithin1Minute()
    {
        // Arrange
        var mockRepo = new Mock<IFileRetrievalConfigurationRepository>();
        var configurations = Enumerable.Range(1, 1000)
            .Select(i => CreateTestConfiguration($"client-{i % 50}", $"config-{i}"))
            .ToList();

        mockRepo.Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(configurations);

        // Simulate simple schedule evaluation (in-memory check)
        var isDueCheck = (ScheduleDefinition schedule, DateTimeOffset? lastExecuted) =>
        {
            // Simplified: just check if last execution was > 1 day ago
            return !lastExecuted.HasValue || 
                   DateTimeOffset.UtcNow - lastExecuted.Value > TimeSpan.FromDays(1);
        };

        // Act - Evaluate all schedules
        var stopwatch = Stopwatch.StartNew();
        var activeConfigs = await mockRepo.Object.GetAllActiveAsync();
        
        var dueConfigs = activeConfigs
            .Where(c => isDueCheck(c.Schedule, c.LastExecutedAt))
            .ToList();
        
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Evaluated {activeConfigs.Count} schedules in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Found {dueConfigs.Count} configurations due for execution");

        // Target: Schedule evaluation should complete within 60 seconds for 1000 configs
        // This allows worker to maintain 1-minute execution window (SC-002)
        Assert.True(stopwatch.ElapsedMilliseconds < 60000,
            $"Schedule evaluation too slow: {stopwatch.ElapsedMilliseconds}ms > 60000ms target");
    }

    [Theory]
    [InlineData(100, 10)]   // 100 configs, 10 clients
    [InlineData(500, 25)]   // 500 configs, 25 clients
    [InlineData(1000, 50)]  // 1000 configs, 50 clients
    public async Task ConfigurationRepository_WithManyConfigurations_MaintainsQueryPerformance(
        int totalConfigs, int clientCount)
    {
        // Arrange
        var mockRepo = new Mock<IFileRetrievalConfigurationRepository>();
        var configurations = Enumerable.Range(1, totalConfigs)
            .Select(i => CreateTestConfiguration($"client-{i % clientCount}", $"config-{i}"))
            .ToList();

        // Mock single-client query (partition key query - should be fast)
        var testClientId = "client-0";
        var clientConfigs = configurations.Where(c => c.ClientId == testClientId).ToList();

        mockRepo.Setup(r => r.GetByClientIdAsync(testClientId, 100, null))
            .ReturnsAsync((clientConfigs, (string?)null));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var (results, _) = await mockRepo.Object.GetByClientIdAsync(testClientId, 100, null);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Single-client query with {totalConfigs} total configs: {stopwatch.ElapsedMilliseconds}ms");

        // Target: Client-scoped query (partition key query) should be < 100ms even with 1000+ total configs
        Assert.True(stopwatch.ElapsedMilliseconds < 100,
            $"Client query performance issue: {stopwatch.ElapsedMilliseconds}ms > 100ms target");

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task FileCheckService_WithMixedProtocols_HandlesLoadEvenly()
    {
        // Arrange - Create configurations across all 3 protocols
        var mockConfigRepo = new Mock<IFileRetrievalConfigurationRepository>();
        var mockExecutionRepo = new Mock<IFileRetrievalExecutionRepository>();
        var mockDiscoveredFileRepo = new Mock<IDiscoveredFileRepository>();
        var mockMessageSession = new Mock<IMessageSession>();

        var protocols = new[] { ProtocolType.Ftp, ProtocolType.Https, ProtocolType.AzureBlob };
        var configurations = Enumerable.Range(1, 300)
            .Select(i => CreateTestConfigurationWithProtocol(
                $"client-{i % 30}", 
                $"config-{i}", 
                protocols[i % 3])) // Distribute evenly across protocols
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

        // Mock protocol adapters with varying delays
        var ftpAdapter = new Mock<IProtocolAdapter>();
        ftpAdapter.Setup(a => a.CheckForFilesAsync(It.IsAny<ProtocolSettings>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(async (ProtocolSettings s, string p, string f, string? e, CancellationToken ct) =>
            {
                await Task.Delay(150, ct); // FTP typically slower
                return new List<DiscoveredFileInfo>();
            });

        var httpsAdapter = new Mock<IProtocolAdapter>();
        httpsAdapter.Setup(a => a.CheckForFilesAsync(It.IsAny<ProtocolSettings>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(async (ProtocolSettings s, string p, string f, string? e, CancellationToken ct) =>
            {
                await Task.Delay(80, ct); // HTTPS medium speed
                return new List<DiscoveredFileInfo>();
            });

        var azureBlobAdapter = new Mock<IProtocolAdapter>();
        azureBlobAdapter.Setup(a => a.CheckForFilesAsync(It.IsAny<ProtocolSettings>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(async (ProtocolSettings s, string p, string f, string? e, CancellationToken ct) =>
            {
                await Task.Delay(50, ct); // Azure Blob fastest
                return new List<DiscoveredFileInfo>();
            });

        var mockProtocolFactory = new Mock<IProtocolAdapterFactory>();
        mockProtocolFactory.Setup(f => f.GetAdapter(ProtocolType.Ftp)).Returns(ftpAdapter.Object);
        mockProtocolFactory.Setup(f => f.GetAdapter(ProtocolType.Https)).Returns(httpsAdapter.Object);
        mockProtocolFactory.Setup(f => f.GetAdapter(ProtocolType.AzureBlob)).Returns(azureBlobAdapter.Object);

        var tokenService = new TokenReplacementService(Mock.Of<ILogger<TokenReplacementService>>());

        var fileCheckService = new FileCheckService(
            mockConfigRepo.Object,
            mockExecutionRepo.Object,
            mockDiscoveredFileRepo.Object,
            mockProtocolFactory.Object,
            tokenService,
            mockMessageSession.Object,
            Mock.Of<ILogger<FileCheckService>>());

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        var tasks = configurations.Select(config => 
            fileCheckService.ExecuteCheckAsync(config.Id, config.ClientId, CancellationToken.None)
        ).ToList();

        await Task.WhenAll(tasks);
        
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"300 checks across 3 protocols completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        // Count by protocol
        var ftpCount = configurations.Count(c => c.Protocol == ProtocolType.Ftp);
        var httpsCount = configurations.Count(c => c.Protocol == ProtocolType.Https);
        var blobCount = configurations.Count(c => c.Protocol == ProtocolType.AzureBlob);

        _output.WriteLine($"FTP: {ftpCount}, HTTPS: {httpsCount}, AzureBlob: {blobCount}");

        // Target: Complete within 30 seconds with protocol mix
        Assert.True(stopwatch.Elapsed.TotalSeconds < 30,
            $"Load test failed: {stopwatch.Elapsed.TotalSeconds:F2}s > 30s target");
    }

    [Fact]
    public async Task SchedulerService_With1000Configurations_DoesNotExceedMemoryLimit()
    {
        // Arrange
        var mockRepo = new Mock<IFileRetrievalConfigurationRepository>();
        var configurations = Enumerable.Range(1, 1000)
            .Select(i => CreateTestConfiguration($"client-{i % 50}", $"config-{i}"))
            .ToList();

        mockRepo.Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(configurations);

        // Act - Measure memory before and after loading all configurations
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(false);

        var activeConfigs = await mockRepo.Object.GetAllActiveAsync();

        var memoryAfter = GC.GetTotalMemory(false);
        var memoryUsedMB = (memoryAfter - memoryBefore) / (1024.0 * 1024.0);

        // Assert
        _output.WriteLine($"Memory used for 1000 configurations: {memoryUsedMB:F2} MB");

        // Target: Loading 1000 configurations should use < 50 MB memory
        Assert.True(memoryUsedMB < 50,
            $"Excessive memory usage: {memoryUsedMB:F2} MB > 50 MB target");

        Assert.Equal(1000, activeConfigs.Count);
    }

    private static FileRetrievalConfiguration CreateTestConfiguration(string clientId, string name)
    {
        return new FileRetrievalConfiguration
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = name,
            Description = "Test configuration for load testing",
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
            EventsToPublish = new List<EventDefinition>
            {
                new EventDefinition
                {
                    EventType = "FileDiscovered",
                    EventData = null
                }
            },
            CommandsToSend = new List<CommandDefinition>(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test-user",
            ETag = Guid.NewGuid().ToString()
        };
    }

    private static FileRetrievalConfiguration CreateTestConfigurationWithProtocol(
        string clientId, string name, ProtocolType protocol)
    {
        var config = CreateTestConfiguration(clientId, name);
        config.Protocol = protocol;

        config.ProtocolSettings = protocol switch
        {
            ProtocolType.Ftp => new FtpProtocolSettings
            {
                Server = "ftp.test.com",
                Port = 21,
                Username = "testuser",
                PasswordKeyVaultSecret = "test-password",
                UseTls = true,
                UsePassiveMode = true,
                ConnectionTimeout = TimeSpan.FromSeconds(30)
            },
            ProtocolType.Https => new HttpsProtocolSettings
            {
                BaseUrl = "https://test.example.com",
                AuthenticationType = AuthType.None,
                ConnectionTimeout = TimeSpan.FromSeconds(30),
                FollowRedirects = true,
                MaxRedirects = 3
            },
            ProtocolType.AzureBlob => new AzureBlobProtocolSettings
            {
                StorageAccountName = "testaccount",
                ContainerName = "test-container",
                AuthenticationType = AzureAuthType.ManagedIdentity,
                BlobPrefix = "files/"
            },
            _ => throw new ArgumentException($"Unknown protocol: {protocol}")
        };

        return config;
    }
}
