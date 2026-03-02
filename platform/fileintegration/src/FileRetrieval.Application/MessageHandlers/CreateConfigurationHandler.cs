using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.FileRetrieval.Application.Services;
using FileRetrieval.Contracts.Commands;
using FileRetrieval.Contracts.Events;
using FileRetrieval.Contracts.DTOs;
using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.ValueObjects;
using System.Text.Json;

namespace RiskInsure.FileRetrieval.Application.MessageHandlers;

/// <summary>
/// Handles CreateConfiguration commands to create new file retrieval configurations.
/// </summary>
public class CreateConfigurationHandler : IHandleMessages<CreateConfiguration>
{
    private readonly ConfigurationService _configurationService;
    private readonly ILogger<CreateConfigurationHandler> _logger;

    public CreateConfigurationHandler(
        ConfigurationService configurationService,
        ILogger<CreateConfigurationHandler> logger)
    {
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task Handle(CreateConfiguration message, IMessageHandlerContext context)
    {
        _logger.LogInformation(
            "Handling CreateConfiguration command for client {ClientId}, configuration {ConfigurationId}",
            message.ClientId,
            message.ConfigurationId);

        try
        {
            // Check if configuration already exists (idempotency)
            var existing = await _configurationService.GetByIdAsync(
                message.ClientId,
                message.ConfigurationId,
                context.CancellationToken);

            if (existing != null)
            {
                _logger.LogInformation(
                    "Configuration {ConfigurationId} already exists for client {ClientId}, skipping create (idempotent)",
                    message.ConfigurationId,
                    message.ClientId);
                return;
            }

            // Map command to entity
            var configuration = MapCommandToEntity(message);

            // Create configuration via service
            var created = await _configurationService.CreateAsync(
                configuration,
                context.CancellationToken);

            // Publish ConfigurationCreated event
            var configurationCreatedEvent = new ConfigurationCreated
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = message.CorrelationId,
                OccurredUtc = DateTimeOffset.UtcNow,
                IdempotencyKey = message.IdempotencyKey,
                ClientId = created.ClientId,
                ConfigurationId = created.Id,
                Name = created.Name,
                Protocol = created.Protocol.ToString(),
                FilePathPattern = created.FilePathPattern,
                FilenamePattern = created.FilenamePattern,
                CronExpression = created.Schedule.CronExpression,
                Timezone = created.Schedule.Timezone,
                IsActive = created.IsActive,
                CreatedBy = created.CreatedBy
            };

            await context.Publish(configurationCreatedEvent);

            _logger.LogInformation(
                "Successfully created configuration {ConfigurationId} for client {ClientId}",
                created.Id,
                created.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create configuration {ConfigurationId} for client {ClientId}",
                message.ConfigurationId,
                message.ClientId);

            // Re-throw to trigger NServiceBus retry logic
            throw;
        }
    }

    private FileRetrievalConfiguration MapCommandToEntity(CreateConfiguration message)
    {
        // Parse protocol type
        if (!Enum.TryParse<ProtocolType>(message.Protocol, ignoreCase: true, out var protocolType))
        {
            throw new ArgumentException($"Invalid protocol type: {message.Protocol}", nameof(message.Protocol));
        }

        // Map protocol settings based on type
        ProtocolSettings protocolSettings = MapProtocolSettings(message.ProtocolSettings, protocolType);

        // Map schedule definition
        var schedule = MapScheduleDefinition(message.Schedule);

        return new FileRetrievalConfiguration
        {
            Id = message.ConfigurationId,
            ClientId = message.ClientId,
            Name = message.Name,
            Description = message.Description,
            Protocol = protocolType,
            ProtocolSettings = protocolSettings,
            FilePathPattern = message.FilePathPattern,
            FilenamePattern = message.FilenamePattern,
            FileExtension = message.FileExtension,
            Schedule = schedule,
            IsActive = true,
            CreatedAt = message.OccurredUtc,
            CreatedBy = message.CreatedBy
        };
    }

    private ProtocolSettings MapProtocolSettings(Dictionary<string, object> settings, ProtocolType protocolType)
    {
        return protocolType switch
        {
            ProtocolType.FTP => MapFtpSettings(settings),
            ProtocolType.HTTPS => MapHttpsSettings(settings),
            ProtocolType.AzureBlob => MapAzureBlobSettings(settings),
            _ => throw new ArgumentException($"Unsupported protocol type: {protocolType}")
        };
    }

    private FtpProtocolSettings MapFtpSettings(Dictionary<string, object> settings)
    {
        return new FtpProtocolSettings(
            server: GetRequiredString(settings, "Server"),
            port: GetInt(settings, "Port", 21),
            username: GetRequiredString(settings, "Username"),
            passwordKeyVaultSecret: GetRequiredString(settings, "PasswordKeyVaultSecret"),
            useTls: GetBool(settings, "UseTls", true),
            usePassiveMode: GetBool(settings, "UsePassiveMode", true),
            connectionTimeout: TimeSpan.FromSeconds(GetInt(settings, "ConnectionTimeoutSeconds", 30))
        );
    }

    private HttpsProtocolSettings MapHttpsSettings(Dictionary<string, object> settings)
    {
        var authTypeStr = GetString(settings, "AuthenticationType") ?? "None";
        if (!Enum.TryParse<AuthType>(authTypeStr, ignoreCase: true, out var authType))
        {
            authType = AuthType.None;
        }

        return new HttpsProtocolSettings(
            baseUrl: GetRequiredString(settings, "BaseUrl"),
            authenticationType: authType,
            usernameOrApiKey: GetString(settings, "UsernameOrApiKey"),
            passwordOrTokenKeyVaultSecret: GetString(settings, "PasswordOrTokenKeyVaultSecret"),
            connectionTimeout: TimeSpan.FromSeconds(GetInt(settings, "ConnectionTimeoutSeconds", 30)),
            followRedirects: GetBool(settings, "FollowRedirects", true),
            maxRedirects: GetInt(settings, "MaxRedirects", 3)
        );
    }

    private AzureBlobProtocolSettings MapAzureBlobSettings(Dictionary<string, object> settings)
    {
        var authTypeStr = GetString(settings, "AuthenticationType") ?? "ManagedIdentity";
        if (!Enum.TryParse<AzureAuthType>(authTypeStr, ignoreCase: true, out var authType))
        {
            authType = AzureAuthType.ManagedIdentity;
        }

        return new AzureBlobProtocolSettings(
            storageAccountName: GetRequiredString(settings, "StorageAccountName"),
            containerName: GetRequiredString(settings, "ContainerName"),
            authenticationType: authType,
            connectionStringKeyVaultSecret: GetString(settings, "ConnectionStringKeyVaultSecret"),
            sasTokenKeyVaultSecret: GetString(settings, "SasTokenKeyVaultSecret"),
            blobPrefix: GetString(settings, "BlobPrefix")
        );
    }

    private static string GetRequiredString(Dictionary<string, object> settings, string key)
    {
        return GetString(settings, key)
            ?? throw new ArgumentException($"Required setting '{key}' is missing or empty", key);
    }

    private static string? GetString(Dictionary<string, object> settings, string key)
    {
        if (!settings.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            JsonElement json when json.ValueKind == JsonValueKind.String => json.GetString(),
            JsonElement json => json.ToString(),
            _ => value.ToString()
        };
    }

    private static int GetInt(Dictionary<string, object> settings, string key, int defaultValue)
    {
        if (!settings.TryGetValue(key, out var value) || value is null)
        {
            return defaultValue;
        }

        return value switch
        {
            JsonElement json when json.ValueKind == JsonValueKind.Number => json.GetInt32(),
            JsonElement json when json.ValueKind == JsonValueKind.String && int.TryParse(json.GetString(), out var parsed) => parsed,
            _ when int.TryParse(value.ToString(), out var parsed) => parsed,
            _ => defaultValue
        };
    }

    private static bool GetBool(Dictionary<string, object> settings, string key, bool defaultValue)
    {
        if (!settings.TryGetValue(key, out var value) || value is null)
        {
            return defaultValue;
        }

        return value switch
        {
            JsonElement json when json.ValueKind is JsonValueKind.True or JsonValueKind.False => json.GetBoolean(),
            JsonElement json when json.ValueKind == JsonValueKind.String && bool.TryParse(json.GetString(), out var parsed) => parsed,
            _ when bool.TryParse(value.ToString(), out var parsed) => parsed,
            _ => defaultValue
        };
    }

    private ScheduleDefinition MapScheduleDefinition(ScheduleDefinitionDto dto)
    {
        return new ScheduleDefinition(
            dto.CronExpression,
            dto.Timezone,
            dto.Description
        );
    }
}
