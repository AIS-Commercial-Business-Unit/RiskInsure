namespace FileRetrieval.Contracts.DTOs;

/// <summary>
/// Defines an event to publish when a file is discovered.
/// </summary>
public record EventDefinitionDto
{
    /// <summary>
    /// Type of event to publish (e.g., "FileDiscovered", custom event types).
    /// This determines which subscribers will receive the event.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Optional custom data to include with the event.
    /// This static metadata is defined in the configuration and included with every event published.
    /// Example: { "fileType": "Transaction", "priority": "High", "department": "Finance" }
    /// </summary>
    public Dictionary<string, object>? EventData { get; init; }
}
