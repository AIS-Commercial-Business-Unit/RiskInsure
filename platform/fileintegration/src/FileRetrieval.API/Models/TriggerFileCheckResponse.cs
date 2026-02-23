namespace RiskInsure.FileRetrieval.API.Models;

/// <summary>
/// Response model for manual file check trigger endpoint.
/// </summary>
public class TriggerFileCheckResponse
{
    /// <summary>
    /// Configuration identifier that was triggered.
    /// </summary>
    public required Guid ConfigurationId { get; init; }
    
    /// <summary>
    /// Unique execution identifier for tracking this file check.
    /// </summary>
    public required Guid ExecutionId { get; init; }
    
    /// <summary>
    /// Timestamp when the trigger was initiated.
    /// </summary>
    public required DateTimeOffset TriggeredAt { get; init; }
    
    /// <summary>
    /// Confirmation message.
    /// </summary>
    public required string Message { get; init; }
}
