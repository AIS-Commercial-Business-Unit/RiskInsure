using FileProcessing.Contracts.Commands;
using Microsoft.Extensions.Logging;
using NServiceBus;

namespace RiskInsure.FileProcessing.Application.MessageHandlers;

/// <summary>
/// Handles ProcessFileChunk commands.
/// </summary>
public class ProcessFileChunkHandler : IHandleMessages<ProcessFileChunk>
{
    private readonly ILogger<ProcessFileChunk> _logger;

    public ProcessFileChunkHandler(ILogger<ProcessFileChunk> logger)
    {
        _logger = logger;
    }

    public Task Handle(ProcessFileChunk message, IMessageHandlerContext context)
    {
        _logger.LogInformation(
            "Processing NACHA row {TraceNumber} (TxnCode: {TransactionCode}, AmountCents: {AmountCents}) for file {Filename} client {ClientId}",
            message.Row.TraceNumber,
            message.Row.TransactionCode,
            message.Row.AmountCents,
            message.Filename,
            message.ClientId);

        return Task.CompletedTask;
    }
}