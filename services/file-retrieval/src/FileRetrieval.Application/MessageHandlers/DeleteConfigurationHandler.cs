using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.FileRetrieval.Application.Services;
using FileRetrieval.Contracts.Commands;
using FileRetrieval.Contracts.Events;

namespace RiskInsure.FileRetrieval.Application.MessageHandlers;

/// <summary>
/// T109: Handles DeleteConfiguration commands to soft-delete file retrieval configurations.
/// Implements soft delete by setting IsActive = false to maintain audit trail.
/// </summary>
public class DeleteConfigurationHandler : IHandleMessages<DeleteConfiguration>
{
    private readonly ConfigurationService _configurationService;
    private readonly ILogger<DeleteConfigurationHandler> _logger;

    public DeleteConfigurationHandler(
        ConfigurationService configurationService,
        ILogger<DeleteConfigurationHandler> logger)
    {
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task Handle(DeleteConfiguration message, IMessageHandlerContext context)
    {
        _logger.LogInformation(
            "Handling DeleteConfiguration command for client {ClientId}, configuration {ConfigurationId}",
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
                    "Configuration {ConfigurationId} not found for client {ClientId} - cannot delete",
                    message.ConfigurationId,
                    message.ClientId);
                
                // Already deleted or never existed - idempotent behavior
                return;
            }

            // T118: Capture before state for structured logging
            var beforeState = new
            {
                existing.Name,
                existing.Protocol,
                existing.IsActive,
                existing.ETag,
                existing.LastExecutedAt
            };

            // T108: Soft delete via service
            var deleted = await _configurationService.DeleteAsync(
                message.ClientId,
                message.ConfigurationId,
                message.DeletedBy,
                context.CancellationToken);

            if (!deleted)
            {
                _logger.LogWarning(
                    "Configuration {ConfigurationId} not found for client {ClientId}",
                    message.ConfigurationId,
                    message.ClientId);
                return;
            }

            // T114: Publish ConfigurationDeleted event
            var configurationDeletedEvent = new ConfigurationDeleted
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = message.CorrelationId,
                OccurredUtc = DateTimeOffset.UtcNow,
                IdempotencyKey = message.IdempotencyKey,
                ClientId = message.ClientId,
                ConfigurationId = message.ConfigurationId,
                Name = beforeState.Name,
                DeletedBy = message.DeletedBy,
                IsSoftDelete = true
            };

            await context.Publish(configurationDeletedEvent);

            // T118: Structured logging with before state
            _logger.LogInformation(
                "Successfully soft-deleted configuration {ConfigurationId} for client {ClientId}. Before: {@Before}",
                message.ConfigurationId,
                message.ClientId,
                beforeState);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete configuration {ConfigurationId} for client {ClientId}",
                message.ConfigurationId,
                message.ClientId);
            throw;
        }
    }
}
