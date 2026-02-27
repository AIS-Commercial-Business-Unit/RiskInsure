using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.FileRetrieval.Application.Services;
using FileRetrieval.Contracts.Commands;
using FileRetrieval.Contracts.Events;
using FileRetrieval.Contracts.DTOs;
using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.Repositories;
using RiskInsure.FileRetrieval.Domain.ValueObjects;
using System.Security.Cryptography;
using System.Text.Json;

namespace RiskInsure.FileRetrieval.Application.MessageHandlers;

/// <summary>
/// Handles ProcessDiscoveredFile commands to process discovered files.
/// </summary>
public class ProcessDiscoveredFileHandler : IHandleMessages<ProcessDiscoveredFile>
{
    private readonly IFileRetrievalConfigurationRepository _configurationRepository;
    private readonly IProcessedFileRecordRepository _processedRecordRepository;
    private readonly DiscoveredFileContentDownloadService _discoveredFileContentDownloadService;
    private readonly ILogger<ProcessDiscoveredFileHandler> _logger;

    public ProcessDiscoveredFileHandler(
        IFileRetrievalConfigurationRepository configurationRepository,
        IProcessedFileRecordRepository processedRecordRepository,
        DiscoveredFileContentDownloadService discoveredFileContentDownloadService,
        ILogger<ProcessDiscoveredFileHandler> logger)
    {
        _configurationRepository = configurationRepository;
        _processedRecordRepository = processedRecordRepository;
        _discoveredFileContentDownloadService = discoveredFileContentDownloadService;
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
            var configuration = await _configurationRepository.GetByIdAsync(
                message.ClientId,
                message.ConfigurationId,
                CancellationToken.None);

            if (configuration == null)
            {
                throw new InvalidOperationException(
                    $"FileRetrievalConfiguration {message.ConfigurationId} was not found for client {message.ClientId}.");
            }

            var fileContent = await _discoveredFileContentDownloadService.DownloadToMemoryAsync(
                configuration,
                message,
                CancellationToken.None);

            _logger.LogInformation(
                "Downloaded discovered file {Filename} ({ByteCount} bytes) for client {ClientId}, configuration {ConfigurationId}",
                message.Filename,
                fileContent.Length,
                message.ClientId,
                message.ConfigurationId);

            var checksumHex = Convert.ToHexString(SHA256.HashData(fileContent));

            var processedAt = DateTimeOffset.UtcNow;
            var processedIdempotencyKey = $"{message.IdempotencyKey}:processed";

            var processedRecord = new ProcessedFileRecord
            {
                Id = message.DiscoveredFileId,
                ClientId = message.ClientId,
                ConfigurationId = message.ConfigurationId,
                ExecutionId = message.ExecutionId,
                DiscoveredFileId = message.DiscoveredFileId,
                FileUrl = message.FileUrl,
                Filename = message.Filename,
                Protocol = message.Protocol,
                DownloadedSizeBytes = fileContent.LongLength,
                ChecksumAlgorithm = "SHA-256",
                ChecksumHex = checksumHex,
                CorrelationId = message.CorrelationId,
                IdempotencyKey = processedIdempotencyKey,
                ProcessedAt = processedAt
            };

            processedRecord.Validate();

            var createdRecord = await _processedRecordRepository.CreateAsync(
                processedRecord,
                CancellationToken.None);

            if (createdRecord == null)
            {
                _logger.LogInformation(
                    "Discovered file {DiscoveredFileId} already processed; skipping duplicate publish for configuration {ConfigurationId}",
                    message.DiscoveredFileId,
                    message.ConfigurationId);
                return;
            }


            await context.Publish(new DiscoveredFileProcessed
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = message.CorrelationId,
                OccurredUtc = processedAt,
                IdempotencyKey = processedIdempotencyKey,
                ClientId = message.ClientId,
                ConfigurationId = message.ConfigurationId,
                ExecutionId = message.ExecutionId,
                DiscoveredFileId = message.DiscoveredFileId,
                FileUrl = message.FileUrl,
                Filename = message.Filename,
                Protocol = message.Protocol,
                DownloadedSizeBytes = fileContent.LongLength,
                ChecksumAlgorithm = "SHA-256",
                ChecksumHex = checksumHex
            });

            _logger.LogInformation(
                "Processed discovered file {Filename} for client {ClientId}, configuration {ConfigurationId}; checksum ({Algorithm})={Checksum}",
                message.Filename,
                message.ClientId,
                message.ConfigurationId,
                "SHA-256",
                checksumHex);
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
