namespace FileRetrieval.Contracts.DTOs;

/// <summary>
/// Defines a command to send when a file is discovered.
/// Commands are sent to specific endpoints (e.g., workflow orchestration platform).
/// </summary>
public record CommandDefinitionDto
{
    /// <summary>
    /// Type of command to send (e.g., "ProcessDiscoveredFile", "StartWorkflow").
    /// This determines which handler will process the command.
    /// </summary>
    public required string CommandType { get; init; }

    /// <summary>
    /// Target endpoint name where the command should be sent.
    /// Example: "WorkflowOrchestrator", "DataProcessingService"
    /// Must match NServiceBus endpoint names configured in routing.
    /// </summary>
    public required string TargetEndpoint { get; init; }

    /// <summary>
    /// Optional custom data to include with the command.
    /// This static metadata is defined in the configuration and included with every command sent.
    /// Example: { "workflowType": "Transaction", "priority": 1, "autoStart": true }
    /// </summary>
    public Dictionary<string, object>? CommandData { get; init; }
}
