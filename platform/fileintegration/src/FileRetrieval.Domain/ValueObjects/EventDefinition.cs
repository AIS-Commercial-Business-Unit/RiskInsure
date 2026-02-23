namespace RiskInsure.FileRetrieval.Domain.ValueObjects;

/// <summary>
/// T028: Event definition for publishing when files are discovered.
/// </summary>
public sealed class EventDefinition
{
    /// <summary>
    /// Event message type (e.g., "FileDiscovered")
    /// </summary>
    public string EventType { get; init; }

    /// <summary>
    /// Additional static event data (optional)
    /// </summary>
    public Dictionary<string, object>? EventData { get; init; }

    public EventDefinition(string eventType, Dictionary<string, object>? eventData = null)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("EventType cannot be empty", nameof(eventType));
        if (eventType.Length > 200)
            throw new ArgumentException("EventType cannot exceed 200 characters", nameof(eventType));

        // Validate EventData size (max 10 KB serialized)
        if (eventData != null)
        {
            var serializedSize = System.Text.Json.JsonSerializer.Serialize(eventData).Length;
            if (serializedSize > 10 * 1024)
                throw new ArgumentException("EventData cannot exceed 10 KB when serialized", nameof(eventData));
        }

        EventType = eventType;
        EventData = eventData;
    }
}
