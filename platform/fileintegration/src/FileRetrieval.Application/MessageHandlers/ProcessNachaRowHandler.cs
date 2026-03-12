using FileRetrieval.Contracts.Commands;
using Microsoft.Extensions.Logging;
using NServiceBus;

namespace RiskInsure.FileRetrieval.Application.MessageHandlers;

/// <summary>
/// Handles ProcessNachaRow commands.
/// </summary>
public class ProcessNachaRowHandler : IHandleMessages<ProcessNachaRow>
{
    private readonly ILogger<ProcessNachaRowHandler> _logger;

    public ProcessNachaRowHandler(ILogger<ProcessNachaRowHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(ProcessNachaRow message, IMessageHandlerContext context)
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