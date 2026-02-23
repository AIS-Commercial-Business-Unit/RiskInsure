using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.FileRetrieval.Application.Services;
using FileRetrieval.Contracts.Commands;
using FileRetrieval.Contracts.Events;
using FileRetrieval.Contracts.DTOs;
using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.ValueObjects;

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
                CreatedBy = created.CreatedBy,
                EventCount = created.EventsToPublish.Count,
                CommandCount = created.CommandsToSend.Count
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

        // T092: Validate that at least one EventDefinition exists
        if (message.EventsToPublish == null || !message.EventsToPublish.Any())
        {
            throw new ArgumentException(
                "At least one event definition is required. Configuration must specify what events to publish when files are discovered.",
                nameof(message.EventsToPublish));
        }

        // Map protocol settings based on type
        ProtocolSettings protocolSettings = MapProtocolSettings(message.ProtocolSettings, protocolType);

        // Map schedule definition
        var schedule = MapScheduleDefinition(message.Schedule);

        // Map event definitions
        var events = message.EventsToPublish
            .Select(MapEventDefinition)
            .ToList();

        // Map command definitions
        var commands = (message.CommandsToSend ?? Enumerable.Empty<CommandDefinitionDto>())
            .Select(MapCommandDefinition)
            .ToList();

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
            EventsToPublish = events,
            CommandsToSend = commands,
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
            server: settings["Server"].ToString()!,
            port: Convert.ToInt32(settings["Port"]),
            username: settings["Username"].ToString()!,
            passwordKeyVaultSecret: settings["PasswordKeyVaultSecret"].ToString()!,
            useTls: Convert.ToBoolean(settings.GetValueOrDefault("UseTls", true)),
            usePassiveMode: Convert.ToBoolean(settings.GetValueOrDefault("UsePassiveMode", true)),
            connectionTimeout: TimeSpan.FromSeconds(Convert.ToInt32(settings.GetValueOrDefault("ConnectionTimeoutSeconds", 30)))
        );
    }

    private HttpsProtocolSettings MapHttpsSettings(Dictionary<string, object> settings)
    {
        var authTypeStr = settings.GetValueOrDefault("AuthenticationType", "None").ToString()!;
        if (!Enum.TryParse<AuthType>(authTypeStr, ignoreCase: true, out var authType))
        {
            authType = AuthType.None;
        }

        return new HttpsProtocolSettings(
            baseUrl: settings["BaseUrl"].ToString()!,
            authenticationType: authType,
            usernameOrApiKey: settings.GetValueOrDefault("UsernameOrApiKey")?.ToString(),
            passwordOrTokenKeyVaultSecret: settings.GetValueOrDefault("PasswordOrTokenKeyVaultSecret")?.ToString(),
            connectionTimeout: TimeSpan.FromSeconds(Convert.ToInt32(settings.GetValueOrDefault("ConnectionTimeoutSeconds", 30))),
            followRedirects: Convert.ToBoolean(settings.GetValueOrDefault("FollowRedirects", true)),
            maxRedirects: Convert.ToInt32(settings.GetValueOrDefault("MaxRedirects", 3))
        );
    }

    private AzureBlobProtocolSettings MapAzureBlobSettings(Dictionary<string, object> settings)
    {
        var authTypeStr = settings.GetValueOrDefault("AuthenticationType", "ManagedIdentity").ToString()!;
        if (!Enum.TryParse<AzureAuthType>(authTypeStr, ignoreCase: true, out var authType))
        {
            authType = AzureAuthType.ManagedIdentity;
        }

        return new AzureBlobProtocolSettings(
            storageAccountName: settings["StorageAccountName"].ToString()!,
            containerName: settings["ContainerName"].ToString()!,
            authenticationType: authType,
            connectionStringKeyVaultSecret: settings.GetValueOrDefault("ConnectionStringKeyVaultSecret")?.ToString(),
            sasTokenKeyVaultSecret: settings.GetValueOrDefault("SasTokenKeyVaultSecret")?.ToString(),
            blobPrefix: settings.GetValueOrDefault("BlobPrefix")?.ToString()
        );
    }

    private ScheduleDefinition MapScheduleDefinition(ScheduleDefinitionDto dto)
    {
        return new ScheduleDefinition(
            dto.CronExpression,
            dto.Timezone,
            dto.Description
        );
    }

    private EventDefinition MapEventDefinition(EventDefinitionDto dto)
    {
        return new EventDefinition(
            dto.EventType,
            dto.EventData
        );
    }

    private CommandDefinition MapCommandDefinition(CommandDefinitionDto dto)
    {
        return new CommandDefinition(
            dto.CommandType,
            dto.TargetEndpoint,
            dto.CommandData
        );
    }
}
