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
/// Handles ProcessDiscoveredFile commands to process discovered files.
/// </summary>
public class ProcessDiscoveredFileHandler : IHandleMessages<ProcessDiscoveredFile>
{
    private readonly ILogger<ProcessDiscoveredFileHandler> _logger;

    public ProcessDiscoveredFileHandler(
        ILogger<ProcessDiscoveredFileHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(ProcessDiscoveredFile message, IMessageHandlerContext context)
    {
        _logger.LogInformation(
            "Handling ProcessDiscoveredFile command for client {ClientId}, configuration {ConfigurationId}",
            message.ClientId,
            message.ConfigurationId);

        try
        {
            // TODO: Implement
            // TODO: Publish event 
            // TODO: Log message saying it was done

            _logger.LogInformation(
                "Processing of discovered files completed for client {ClientId}, configuration {ConfigurationId} NOT YET IMPLEMENTED",
                message.ClientId,
                message.ConfigurationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process discovered files for client {ClientId}, configuration {ConfigurationId}",
                message.ClientId,
                message.ConfigurationId);

            // Re-throw to trigger NServiceBus retry logic
            throw;
        }
    }
}
