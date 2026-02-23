namespace RiskInsure.FileRetrieval.Domain.ValueObjects;

/// <summary>
/// T029: Command definition for sending commands to workflow orchestration platform when files are discovered.
/// </summary>
public sealed class CommandDefinition
{
    /// <summary>
    /// Command message type (e.g., "ProcessFile")
    /// </summary>
    public string CommandType { get; init; }

    /// <summary>
    /// NServiceBus endpoint name to send command to
    /// </summary>
    public string TargetEndpoint { get; init; }

    /// <summary>
    /// Additional static command data (optional)
    /// </summary>
    public Dictionary<string, object>? CommandData { get; init; }

    public CommandDefinition(string commandType, string targetEndpoint, Dictionary<string, object>? commandData = null)
    {
        if (string.IsNullOrWhiteSpace(commandType))
            throw new ArgumentException("CommandType cannot be empty", nameof(commandType));
        if (commandType.Length > 200)
            throw new ArgumentException("CommandType cannot exceed 200 characters", nameof(commandType));
        if (string.IsNullOrWhiteSpace(targetEndpoint))
            throw new ArgumentException("TargetEndpoint cannot be empty", nameof(targetEndpoint));
        if (targetEndpoint.Length > 200)
            throw new ArgumentException("TargetEndpoint cannot exceed 200 characters", nameof(targetEndpoint));

        // Validate CommandData size (max 10 KB serialized)
        if (commandData != null)
        {
            var serializedSize = System.Text.Json.JsonSerializer.Serialize(commandData).Length;
            if (serializedSize > 10 * 1024)
                throw new ArgumentException("CommandData cannot exceed 10 KB when serialized", nameof(commandData));
        }

        CommandType = commandType;
        TargetEndpoint = targetEndpoint;
        CommandData = commandData;
    }
}
