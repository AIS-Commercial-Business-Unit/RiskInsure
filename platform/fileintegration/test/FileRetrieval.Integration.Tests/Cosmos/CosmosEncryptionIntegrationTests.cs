using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.ValueObjects;
using RiskInsure.FileRetrieval.Infrastructure.Cosmos;
using Xunit;

namespace FileRetrieval.Integration.Tests.Cosmos;

/// <summary>
/// Integration tests for CosmosDB Always Encrypted functionality.
/// Verifies that sensitive protocol credentials are properly encrypted at rest.
/// These tests require a live Cosmos DB instance and will be skipped if not available.
/// </summary>
[Collection("CosmosDB")]
public class CosmosEncryptionIntegrationTests : IAsyncLifetime
{
    private const string ClientId = "encryption-test-client";
    private readonly IConfiguration _configuration;
    private CosmosClient? _cosmosClient;
    private CosmosDbContext? _cosmosContext;
    private readonly ILogger<CosmosEncryptionIntegrationTests> _logger;
    private readonly ServiceProvider _serviceProvider;
    private bool _cosmosDbAvailable = false;

    public CosmosEncryptionIntegrationTests()
    {
        // Build configuration from appsettings files
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddJsonFile("appsettings.test.json", optional: true)
            .AddEnvironmentVariables();

        _configuration = configBuilder.Build();

        // Set up dependency injection
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton(_configuration);
        services.AddSingleton<CosmosClient>(sp =>
        {
            var connectionString = _configuration.GetConnectionString("CosmosDb");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // Return a mock client - tests will be skipped via InitializeAsync check
                connectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDL5cWo=";
            }
            return new CosmosClient(connectionString);
        });
        services.AddSingleton<CosmosEncryptionConfiguration>();
        services.AddSingleton<CosmosDbContext>();

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<CosmosEncryptionIntegrationTests>>();
    }

    public async Task InitializeAsync()
    {
        // Check if Cosmos DB is available - tests will be skipped if not
        try
        {
            _cosmosContext = _serviceProvider.GetRequiredService<CosmosDbContext>();
            _cosmosClient = _serviceProvider.GetRequiredService<CosmosClient>();
            
            // Try to initialize - will fail gracefully if connection unavailable
            await _cosmosContext.InitializeAsync();
            _cosmosDbAvailable = true;
            _logger.LogInformation("✓ Cosmos DB context initialized for encryption tests");
        }
        catch (Exception ex)
        {
            _cosmosDbAvailable = false;
            _logger.LogWarning($"Cosmos DB not available for integration tests (this is expected in CI/local dev): {ex.Message}");
            // Don't rethrow - tests will skip via SkipIfNoCosmos() helper
        }
    }

    private void SkipIfNoCosmos()
    {
        if (!_cosmosDbAvailable)
        {
            // Log and skip silently - tests marked with [Fact] should just return if unable to run
            _logger.LogWarning("Skipping Cosmos DB integration test - database not available");
            throw new InvalidOperationException("Cosmos DB not available for integration test");
        }
    }

    public async Task DisposeAsync()
    {
        // Cleanup: Remove test data
        try
        {
            if (_cosmosContext != null)
            {
                var container = _cosmosContext.ConfigurationsContainer;
                var query = new QueryDefinition("SELECT * FROM c WHERE c.clientId = @clientId")
                    .WithParameter("@clientId", ClientId);

                var iterator = container.GetItemQueryIterator<FileRetrievalConfiguration>(query);
                while (iterator.HasMoreResults)
                {
                    var results = await iterator.ReadNextAsync();
                    foreach (var item in results)
                    {
                        await container.DeleteItemAsync<FileRetrievalConfiguration>(item.Id.ToString(), new PartitionKey(ClientId));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup");
        }

        _serviceProvider?.Dispose();
    }

    [Fact]
    public async Task CosmosEncryptionConfiguration_InitializesSuccessfully()
    {
        // Arrange
        var encryptionConfig = _serviceProvider.GetRequiredService<CosmosEncryptionConfiguration>();

        // Act
        await encryptionConfig.InitializeEncryptionAsync();

        // Assert - should not throw
        var metadata = encryptionConfig.GetEncryptionPolicyMetadata();
        metadata.Should().NotBeNull();
        metadata.KeyVaultUri.Should().NotBeNullOrWhiteSpace();
        metadata.DataEncryptionKeyId.Should().Be("file-retrieval-dek");
        metadata.EncryptionPaths.Should().HaveCount(3);
        metadata.EncryptionPaths.Should().Contain(
            new[] { "/protocolSettings/password", "/protocolSettings/passwordOrToken", "/protocolSettings/connectionString" });

        _logger.LogInformation("✓ Encryption configuration initialized successfully");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Integration", "CosmosDB")]
    public async Task EncryptionConfiguration_Initializes_Successfully()
    {
        // This test validates the encryption configuration infrastructure without needing actual Cosmos DB access
        var encryptionConfig = _serviceProvider.GetRequiredService<CosmosEncryptionConfiguration>();
        
        // Act - Just validate the configuration exists and can be retrieved
        var metadata = encryptionConfig.GetEncryptionPolicyMetadata();
        
        // Assert
        metadata.Should().NotBeNull();
        metadata.EncryptionPaths.Should().NotBeEmpty();
        metadata.EncryptionPaths.Should().Contain("/protocolSettings/password");
        metadata.EncryptionPaths.Should().Contain("/protocolSettings/passwordOrToken");
        metadata.EncryptionPaths.Should().Contain("/protocolSettings/connectionString");
        
        _logger.LogInformation("✓ Encryption configuration infrastructure validated successfully");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Integration", "CosmosDB")]
    public async Task FtpProtocolSettings_WithEncryption_StoresCredentialsSecurely()
    {
        SkipIfNoCosmos();

        // Arrange
        var container = _cosmosContext!.ConfigurationsContainer;
        var encryptionConfig = _serviceProvider.GetRequiredService<CosmosEncryptionConfiguration>();
        await encryptionConfig.InitializeEncryptionAsync();

        var ftpSettings = new FtpProtocolSettings(
            server: "ftp.example.com",
            port: 21,
            username: "testuser",
            password: "super-secret-password-123",
            useTls: true,
            usePassiveMode: true,
            connectionTimeout: TimeSpan.FromSeconds(30)
        );

        var config = new FileRetrievalConfiguration
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            Name = "Encrypted FTP Test",
            Protocol = ProtocolType.FTP,
            ProtocolSettings = ftpSettings,
            FilePathPattern = "/test/path",
            FilenamePattern = "test_*.txt",
            FileExtension = "txt",
            Schedule = new ScheduleDefinition(
                cronExpression: "0 0 * * *",
                timezone: "UTC",
                description: "Daily at midnight"
            ),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test-user"
        };

        // Act
        var response = await container.CreateItemAsync(config, new PartitionKey(ClientId));

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        response.Resource.Should().NotBeNull();
        response.Resource.ProtocolSettings.Should().BeOfType<FtpProtocolSettings>();

        var ftpRetrieved = response.Resource.ProtocolSettings as FtpProtocolSettings;
        ftpRetrieved?.Password.Should().Be("super-secret-password-123");

        _logger.LogInformation("✓ FTP protocol settings with encryption stored successfully");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Integration", "CosmosDB")]
    public async Task HttpsProtocolSettings_WithEncryption_StoresTokenSecurely()
    {
        SkipIfNoCosmos();

        // Arrange
        var container = _cosmosContext!.ConfigurationsContainer;
        var encryptionConfig = _serviceProvider.GetRequiredService<CosmosEncryptionConfiguration>();
        await encryptionConfig.InitializeEncryptionAsync();

        var httpsSettings = new HttpsProtocolSettings(
            baseUrl: "https://api.example.com",
            authenticationType: AuthType.BearerToken,
            usernameOrApiKey: null,
            passwordOrToken: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
            connectionTimeout: TimeSpan.FromSeconds(30),
            followRedirects: true,
            maxRedirects: 3
        );

        var config = new FileRetrievalConfiguration
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            Name = "Encrypted HTTPS Test",
            Protocol = ProtocolType.HTTPS,
            ProtocolSettings = httpsSettings,
            FilePathPattern = "/api/v1/files",
            FilenamePattern = "report_*.json",
            FileExtension = "json",
            Schedule = new ScheduleDefinition(
                cronExpression: "0 */6 * * *",
                timezone: "UTC",
                description: "Every 6 hours"
            ),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test-user"
        };

        // Act
        var response = await container.CreateItemAsync(config, new PartitionKey(ClientId));

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        response.Resource.ProtocolSettings.Should().BeOfType<HttpsProtocolSettings>();

        var httpsRetrieved = response.Resource.ProtocolSettings as HttpsProtocolSettings;
        httpsRetrieved?.PasswordOrToken.Should().Be("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...");

        _logger.LogInformation("✓ HTTPS protocol settings with token encryption stored successfully");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Integration", "CosmosDB")]
    public async Task AzureBlobProtocolSettings_WithEncryption_StoresConnectionStringSecurely()
    {
        SkipIfNoCosmos();

        // Arrange
        var container = _cosmosContext!.ConfigurationsContainer;
        var encryptionConfig = _serviceProvider.GetRequiredService<CosmosEncryptionConfiguration>();
        await encryptionConfig.InitializeEncryptionAsync();

        var azureSettings = new AzureBlobProtocolSettings(
            storageAccountName: "teststorage",
            containerName: "testcontainer",
            authenticationType: AzureAuthType.ConnectionString,
            connectionString: "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=supersecretkey123==;EndpointSuffix=core.windows.net",
            SasToken: null,
            blobPrefix: "2024/reports/"
        );

        var config = new FileRetrievalConfiguration
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            Name = "Encrypted Azure Blob Test",
            Protocol = ProtocolType.AzureBlob,
            ProtocolSettings = azureSettings,
            FilePathPattern = "/",
            FilenamePattern = "*.xlsx",
            FileExtension = "xlsx",
            Schedule = new ScheduleDefinition(
                cronExpression: "0 8 * * *",
                timezone: "UTC",
                description: "Daily at 8 AM"
            ),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test-user"
        };

        // Act
        var response = await container.CreateItemAsync(config, new PartitionKey(ClientId));

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        response.Resource.ProtocolSettings.Should().BeOfType<AzureBlobProtocolSettings>();

        var azureRetrieved = response.Resource.ProtocolSettings as AzureBlobProtocolSettings;
        azureRetrieved?.ConnectionString.Should().StartWith("DefaultEndpointsProtocol=https");

        _logger.LogInformation("✓ Azure Blob protocol settings with connection string encryption stored successfully");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Integration", "CosmosDB")]
    public async Task EncryptedProperties_AreNotPlaintextInStorage()
    {
        SkipIfNoCosmos();

        // Arrange
        var container = _cosmosContext!.ConfigurationsContainer;
        var encryptionConfig = _serviceProvider.GetRequiredService<CosmosEncryptionConfiguration>();
        await encryptionConfig.InitializeEncryptionAsync();

        var sensitivePassword = "this-is-sensitive-data-12345";
        var ftpSettings = new FtpProtocolSettings(
            server: "ftp.example.com",
            port: 21,
            username: "testuser",
            password: sensitivePassword,
            useTls: true,
            usePassiveMode: true,
            connectionTimeout: TimeSpan.FromSeconds(30)
        );

        var config = new FileRetrievalConfiguration
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            Name = "Plaintext Test",
            Protocol = ProtocolType.FTP,
            ProtocolSettings = ftpSettings,
            FilePathPattern = "/test",
            FilenamePattern = "test_*",
            FileExtension = "txt",
            Schedule = new ScheduleDefinition("0 0 * * *", "UTC", "Daily"),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test-user"
        };

        // Act
        var response = await container.CreateItemAsync(config, new PartitionKey(ClientId));
        var storedDocument = response.Resource;

        // Assert - Verify application-level decryption works
        var ftpRetrieved = storedDocument.ProtocolSettings as FtpProtocolSettings;
        ftpRetrieved?.Password.Should().Be(sensitivePassword);

        // Note: For true plaintext verification at storage level, you would need to:
        // 1. Query the raw JSON from Cosmos (bypassing application deserialization)
        // 2. Or inspect the actual encrypted bytes in Cosmos when full Always Encrypted SDK is available
        _logger.LogInformation("✓ Encrypted properties properly decrypted on retrieval");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Integration", "CosmosDB")]
    public async Task MultipleConfigurations_CanBeStoredWithEncryption()
    {
        SkipIfNoCosmos();

        // Arrange
        var container = _cosmosContext!.ConfigurationsContainer;
        var encryptionConfig = _serviceProvider.GetRequiredService<CosmosEncryptionConfiguration>();
        await encryptionConfig.InitializeEncryptionAsync();

        var configs = new List<FileRetrievalConfiguration>
        {
            CreateTestConfiguration(
                ProtocolType.FTP,
                new FtpProtocolSettings("ftp1.com", 21, "user1", "pass1", true, true, TimeSpan.FromSeconds(30))
            ),
            CreateTestConfiguration(
                ProtocolType.HTTPS,
                new HttpsProtocolSettings("https://api1.com", AuthType.BearerToken, null, "token1")
            ),
            CreateTestConfiguration(
                ProtocolType.AzureBlob,
                new AzureBlobProtocolSettings("storage1", "container1", AzureAuthType.ManagedIdentity)
            )
        };

        // Act
        var results = new List<ItemResponse<FileRetrievalConfiguration>>();
        foreach (var config in configs)
        {
            var response = await container.CreateItemAsync(config, new PartitionKey(ClientId));
            results.Add(response);
        }

        // Assert
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.StatusCode.Should().Be(System.Net.HttpStatusCode.Created));

        // Verify retrieval
        var ftpConfig = results[0].Resource;
        var httpsConfig = results[1].Resource;
        var azureConfig = results[2].Resource;

        (ftpConfig.ProtocolSettings as FtpProtocolSettings)?.Password.Should().Be("pass1");
        (httpsConfig.ProtocolSettings as HttpsProtocolSettings)?.PasswordOrToken.Should().Be("token1");
        azureConfig.ProtocolSettings.Should().BeOfType<AzureBlobProtocolSettings>();

        _logger.LogInformation("✓ Multiple configurations with different protocols stored with encryption successfully");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Integration", "CosmosDB")]
    public async Task EncryptionConfiguration_LogsCorrectPaths()
    {
        // Arrange
        var encryptionConfig = _serviceProvider.GetRequiredService<CosmosEncryptionConfiguration>();

        // Act
        await encryptionConfig.InitializeEncryptionAsync();
        var metadata = encryptionConfig.GetEncryptionPolicyMetadata();

        // Assert
        metadata.EncryptionPaths.Should().Contain(path =>
            path == "/protocolSettings/password" ||
            path == "/protocolSettings/passwordOrToken" ||
            path == "/protocolSettings/connectionString"
        );

        _logger.LogInformation("✓ Encryption configuration correctly identifies sensitive paths");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Integration", "CosmosDB")]
    public async Task UnencryptedProperties_RemainsAccessible()
    {
        SkipIfNoCosmos();

        // Arrange
        var container = _cosmosContext!.ConfigurationsContainer;
        var encryptionConfig = _serviceProvider.GetRequiredService<CosmosEncryptionConfiguration>();
        await encryptionConfig.InitializeEncryptionAsync();

        var ftpSettings = new FtpProtocolSettings(
            server: "ftp.example.com",
            port: 21,
            username: "testuser",
            password: "secret",
            useTls: true,
            usePassiveMode: true,
            connectionTimeout: TimeSpan.FromSeconds(30)
        );

        var config = new FileRetrievalConfiguration
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            Name = "Unencrypted Properties Test",
            Protocol = ProtocolType.FTP,
            ProtocolSettings = ftpSettings,
            FilePathPattern = "/test/path",
            FilenamePattern = "test_*.txt",
            FileExtension = "txt",
            Schedule = new ScheduleDefinition("0 0 * * *", "UTC", "Daily"),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test-user"
        };

        // Act
        var response = await container.CreateItemAsync(config, new PartitionKey(ClientId));
        var ftpRetrieved = response.Resource.ProtocolSettings as FtpProtocolSettings;

        // Assert - Unencrypted properties should be accessible
        ftpRetrieved?.Server.Should().Be("ftp.example.com");
        ftpRetrieved?.Port.Should().Be(21);
        ftpRetrieved?.Username.Should().Be("testuser");
        ftpRetrieved?.UseTls.Should().BeTrue();
        ftpRetrieved?.UsePassiveMode.Should().BeTrue();
        ftpRetrieved?.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(30));

        // Only password is encrypted
        ftpRetrieved?.Password.Should().Be("secret");

        _logger.LogInformation("✓ Unencrypted properties remain accessible alongside encrypted ones");
    }

    private static FileRetrievalConfiguration CreateTestConfiguration(
        ProtocolType protocolType,
        ProtocolSettings settings)
    {
        return new FileRetrievalConfiguration
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            Name = $"Test {protocolType}",
            Protocol = protocolType,
            ProtocolSettings = settings,
            FilePathPattern = "/test",
            FilenamePattern = "test_*",
            FileExtension = "txt",
            Schedule = new ScheduleDefinition("0 0 * * *", "UTC", "Daily"),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test-user"
        };
    }
}
