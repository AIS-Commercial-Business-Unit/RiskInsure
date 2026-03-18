using Microsoft.Extensions.Logging;
using NServiceBus;
using RiskInsure.FileProcessing.Application.Services;
using FileProcessing.Contracts.Commands;
using FileProcessing.Contracts.Events;
using FileProcessing.Contracts.DTOs;
using RiskInsure.FileProcessing.Domain.Entities;
using RiskInsure.FileProcessing.Domain.Enums;
using RiskInsure.FileProcessing.Domain.Repositories;
using RiskInsure.FileProcessing.Domain.ValueObjects;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RiskInsure.FileProcessing.Application.MessageHandlers;

/// <summary>
/// Handles ProcessDiscoveredFile commands to process discovered files.
/// </summary>
public class ProcessDiscoveredFileHandler : IHandleMessages<ProcessDiscoveredFile>
{
    private readonly IFileProcessingConfigurationRepository _configurationRepository;
    private readonly IProcessedFileRecordRepository _processedRecordRepository;
    private readonly DiscoveredFileContentDownloadService _discoveredFileContentDownloadService;
    private readonly ILogger<ProcessDiscoveredFileHandler> _logger;

    public ProcessDiscoveredFileHandler(
        IFileProcessingConfigurationRepository configurationRepository,
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
                    $"FileProcessingConfiguration {message.ConfigurationId} was not found for client {message.ClientId}.");
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

            await ProcessFileByTypeAsync(configuration, message, fileContent, context);


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

    private async Task ProcessFileByTypeAsync(
        FileProcessingConfiguration configuration,
        ProcessDiscoveredFile message,
        byte[] fileContent,
        IMessageHandlerContext context)
    {
        var fileType = configuration.ProcessingConfig?.FileType;

        if (string.Equals(fileType, "NACHA", StringComparison.OrdinalIgnoreCase))
        {
            await ProcessNachaFileAsync(message, fileContent, context);
            return;
        }

        _logger.LogInformation(
            "No row-level processor configured for file type {FileType} on configuration {ConfigurationId}",
            fileType ?? "<null>",
            configuration.Id);
    }

    private async Task ProcessNachaFileAsync(
        ProcessDiscoveredFile message,
        byte[] fileContent,
        IMessageHandlerContext context)
    {
        var nachaRows = ParseNachaRows(fileContent);

        _logger.LogInformation(
            "Parsed {RowCount} NACHA rows from file {Filename} for client {ClientId}",
            nachaRows.Count,
            message.Filename,
            message.ClientId);

        foreach (var row in nachaRows)
        {
            var rowIdempotencyKey = $"{message.IdempotencyKey}:nacha:{row.TraceNumber}";

            await context.Publish(new NachaRowDiscovered
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = message.CorrelationId,
                OccurredUtc = DateTimeOffset.UtcNow,
                IdempotencyKey = rowIdempotencyKey,
                ClientId = message.ClientId,
                ConfigurationId = message.ConfigurationId,
                ExecutionId = message.ExecutionId,
                DiscoveredFileId = message.DiscoveredFileId,
                Filename = message.Filename,
                Row = row
            });

            // Send to the configured endpoint for row handling.
            await context.Send(new ProcessNachaRow
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = message.CorrelationId,
                OccurredUtc = DateTimeOffset.UtcNow,
                IdempotencyKey = rowIdempotencyKey,
                ClientId = message.ClientId,
                ConfigurationId = message.ConfigurationId,
                ExecutionId = message.ExecutionId,
                DiscoveredFileId = message.DiscoveredFileId,
                Filename = message.Filename,
                Row = row
            });
        }
    }

    private static IReadOnlyList<NachaRow> ParseNachaRows(byte[] fileContent)
    {
        var rows = new List<NachaRow>();
        var content = Encoding.ASCII.GetString(fileContent);
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.Length < 94 || line[0] != '6')
            {
                continue;
            }

            var amountText = line.Substring(29, 10);
            if (!long.TryParse(amountText, NumberStyles.None, CultureInfo.InvariantCulture, out var amountCents))
            {
                amountCents = 0;
            }

            var routingPrefix = line.Substring(3, 8);
            var routingCheckDigit = line.Substring(11, 1);

            rows.Add(new NachaRow
            {
                RowNumber = index + 1,
                TransactionCode = line.Substring(1, 2),
                RoutingNumber = routingPrefix + routingCheckDigit,
                AccountNumber = line.Substring(12, 17).Trim(),
                AmountCents = amountCents,
                IndividualId = line.Substring(39, 15).Trim(),
                IndividualName = line.Substring(54, 22).Trim(),
                TraceNumber = line.Substring(79, 15),
                RawRecord = line
            });
        }

        return rows;
    }
}
