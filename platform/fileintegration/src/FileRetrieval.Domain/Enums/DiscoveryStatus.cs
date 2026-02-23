namespace RiskInsure.FileRetrieval.Domain.Enums;

/// <summary>
/// Discovery status for discovered files.
/// T020: DiscoveryStatus enum
/// </summary>
public enum DiscoveryStatus
{
    /// <summary>File discovered and pending processing</summary>
    Discovered = 1,
    
    /// <summary>Event published for this file</summary>
    EventPublished = 2,
    
    /// <summary>Command sent for this file</summary>
    CommandSent = 3,
    
    /// <summary>Processing completed</summary>
    Completed = 4,
    
    /// <summary>Processing failed</summary>
    Failed = 5
}
