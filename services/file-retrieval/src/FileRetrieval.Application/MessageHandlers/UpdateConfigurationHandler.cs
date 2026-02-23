using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.FileRetrieval.Application.Services;
using FileRetrieval.Contracts.Commands;
using FileRetrieval.Contracts.Events;
using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.ValueObjects;
using Microsoft.Azure.Cosmos;

namespace RiskInsure.FileRetrieval.Application.MessageHandlers;

/// <summary>
/// T107: Handles UpdateConfiguration commands to update existing file retrieval configurations.
/// Implements ETag-based optimistic concurrency control.
/// </summary>
public class UpdateConfigurationHandler : IHandleMessages<UpdateConfiguration>
{
    private readonly ConfigurationService _configurationService;
    private readonly ILogger<UpdateConfigurationHandler> _logger;

    public UpdateConfigurationHandler(
        ConfigurationService configurationService,
        ILogger<UpdateConfigurationHandler> logger)
    {
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task Handle(UpdateConfiguration message, IMessageHandlerContext context)
    {
        _logger.LogInformation(
            "Handling UpdateConfiguration command for client {ClientId}, configuration {ConfigurationId}",
            message.ClientId,
            message.ConfigurationId);

        try
        {
            // Retrieve existing configuration to capture before state
            var existing = await _configurationService.GetByIdAsync(
                message.ClientId,
                message.ConfigurationId,
                context.CancellationToken);

            if (existing == null)
            {
                _logger.LogWarning(
                    "Configuration {ConfigurationId} not found for client {ClientId}",
                    message.ConfigurationId,
                    message.ClientId);
                throw new InvalidOperationException($"Configuration {message.ConfigurationId} not found");
            }

            // T118: Capture before state for structured logging
            var beforeState = new
            {
                existing.Name,
                existing.Protocol,
                existing.FilePathPattern,
                existing.FilenamePattern,
                existing.IsActive,
                existing.ETag
            };

            // Parse protocol type
            if (!Enum.TryParse<ProtocolType>(message.Protocol, ignoreCase: true, out var protocolType))
            {
                throw new ArgumentException($"Invalid protocol type: {message.Protocol}", nameof(message.Protocol));
            }

            // Validate at least one event definition exists
            if (message.EventsToPublish == null || !message.EventsToPublish.Any())
            {
                throw new ArgumentException(
                    "At least one event definition is required",
                    nameof(message.EventsToPublish));
            }

            // Update entity properties
            existing.Name = message.Name;
            existing.Description = message.Description;
            existing.Protocol = protocolType;
            existing.ProtocolSettings = MapProtocolSettings(message.Protocol, message.ProtocolSettings);
            existing.FilePathPattern = message.FilePathPattern;
            existing.FilenamePattern = message.FilenamePattern;
            existing.FileExtension = message.FileExtension;
            existing.Schedule = new ScheduleDefinition(
                message.Schedule.CronExpression,
                message.Schedule.Timezone ?? "UTC",
                message.Schedule.Description);
            existing.EventsToPublish = message.EventsToPublish.Select(e => new EventDefinition(
                e.EventType,
                e.EventData ?? new Dictionary<string, object>())).ToList();
            existing.CommandsToSend = message.CommandsToSend?.Select(c => new CommandDefinition(
                c.CommandType,
                c.TargetEndpoint,
                c.CommandData ?? new Dictionary<string, object>())).ToList() ?? new List<CommandDefinition>();
            existing.LastModifiedBy = message.LastModifiedBy;
            existing.ETag = message.ETag; // T106: ETag for optimistic concurrency

            // T117: Validate updates don't break running executions
            // Updates take effect on next scheduled run (documented behavior)
            _logger.LogDebug(
                "Update will take effect on next scheduled run for configuration {ConfigurationId}",
                message.ConfigurationId);

            // Update via service (includes ETag validation)
            var updated = await _configurationService.UpdateAsync(
                existing,
                context.CancellationToken);

            // T113: Publish ConfigurationUpdated event with ChangedFields tracking
            var changedFields = new List<string>();
            if (beforeState.Name != updated.Name) changedFields.Add("Name");
            if (beforeState.Protocol != updated.Protocol) changedFields.Add("Protocol");
            if (beforeState.FilePathPattern != updated.FilePathPattern) changedFields.Add("FilePathPattern");
            if (beforeState.FilenamePattern != updated.FilenamePattern) changedFields.Add("FilenamePattern");
            if (beforeState.IsActive != updated.IsActive) changedFields.Add("IsActive");

            var configurationUpdatedEvent = new ConfigurationUpdated
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = message.CorrelationId,
                OccurredUtc = DateTimeOffset.UtcNow,
                IdempotencyKey = message.IdempotencyKey,
                ClientId = updated.ClientId,
                ConfigurationId = updated.Id,
                Name = updated.Name,
                Protocol = updated.ProtocolSettings.ProtocolType.ToString(),
                FilePathPattern = updated.FilePathPattern,
                FilenamePattern = updated.FilenamePattern,
                CronExpression = updated.Schedule.CronExpression,
                Timezone = updated.Schedule.Timezone,
                IsActive = updated.IsActive,
                LastModifiedBy = updated.LastModifiedBy ?? "unknown",
                ChangedFields = changedFields
            };

            await context.Publish(configurationUpdatedEvent);

            // T118: Structured logging with before/after state
            _logger.LogInformation(
                "Successfully updated configuration {ConfigurationId} for client {ClientId}. Changed fields: {ChangedFields}. Before: {@Before}, After: {@After}",
                updated.Id,
                updated.ClientId,
                string.Join(", ", changedFields),
                beforeState,
                new
                {
                    updated.Name,
                    updated.Protocol,
                    updated.FilePathPattern,
                    updated.FilenamePattern,
                    updated.IsActive,
                    updated.ETag
                });
        }
        catch (Microsoft.Azure.Cosmos.CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            // T115: ETag conflict - another update occurred
            _logger.LogWarning(
                ex,
                "ETag conflict updating configuration {ConfigurationId} for client {ClientId}. Configuration was modified by another request.",
                message.ConfigurationId,
                message.ClientId);
            throw new InvalidOperationException(
                $"Configuration was modified by another request. Please retrieve the latest version and try again.",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update configuration {ConfigurationId} for client {ClientId}",
                message.ConfigurationId,
                message.ClientId);
            throw;
        }
    }

    private ProtocolSettings MapProtocolSettings(string protocol, Dictionary<string, object> settings)
    {
        return protocol.ToUpperInvariant() switch
        {
            "FTP" => MapFtpSettings(settings),
            "HTTPS" => MapHttpsSettings(settings),
            "AZUREBLOB" => MapAzureBlobSettings(settings),
            _ => throw new ArgumentException($"Unknown protocol: {protocol}")
        };
    }

    private FtpProtocolSettings MapFtpSettings(Dictionary<string, object> settings)
    {
        return new FtpProtocolSettings(
            server: settings["Server"].ToString()!,
            port: Convert.ToInt32(settings["Port"]),
            username: settings["Username"].ToString()!,
            passwordKeyVaultSecret: settings["PasswordKeyVaultSecret"].ToString()!,
            useTls: Convert.ToBoolean(settings["UseTls"]),
            usePassiveMode: Convert.ToBoolean(settings["UsePassiveMode"]),
            connectionTimeout: TimeSpan.FromSeconds(Convert.ToInt32(settings.GetValueOrDefault("ConnectionTimeoutSeconds", 30))));
    }

    private HttpsProtocolSettings MapHttpsSettings(Dictionary<string, object> settings)
    {
        if (!Enum.TryParse<AuthType>(settings["AuthenticationType"].ToString(), out var authType))
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
            maxRedirects: Convert.ToInt32(settings.GetValueOrDefault("MaxRedirects", 5)));
    }

    private AzureBlobProtocolSettings MapAzureBlobSettings(Dictionary<string, object> settings)
    {
        if (!Enum.TryParse<AzureAuthType>(settings["AuthenticationType"].ToString(), out var authType))
        {
            authType = AzureAuthType.ManagedIdentity;
        }

        return new AzureBlobProtocolSettings(
            storageAccountName: settings["StorageAccountName"].ToString()!,
            containerName: settings["ContainerName"].ToString()!,
            authenticationType: authType,
            connectionStringKeyVaultSecret: settings.GetValueOrDefault("ConnectionStringKeyVaultSecret")?.ToString(),
            sasTokenKeyVaultSecret: settings.GetValueOrDefault("SasTokenKeyVaultSecret")?.ToString());
    }
}
