using FileRetrieval.Contracts.DTOs;

namespace RiskInsure.FileRetrieval.API.Models;

/// <summary>
/// Request model for creating a new file retrieval configuration.
/// </summary>
public class CreateConfigurationRequest
{
    /// <summary>
    /// Human-readable configuration name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of the configuration purpose
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Protocol type ("Ftp", "Https", "AzureBlob")
    /// </summary>
    public required string Protocol { get; set; }

    /// <summary>
    /// Protocol-specific connection settings (dynamic based on Protocol)
    /// </summary>
    public required Dictionary<string, object> ProtocolSettings { get; set; }

    /// <summary>
    /// Path pattern with optional date tokens (e.g., "/data/{yyyy}/{mm}")
    /// </summary>
    public required string FilePathPattern { get; set; }

    /// <summary>
    /// Filename pattern with optional date tokens (e.g., "report_{yyyy}{mm}{dd}.*")
    /// </summary>
    public required string FilenamePattern { get; set; }

    /// <summary>
    /// File extension filter (e.g., "xlsx", "pdf")
    /// </summary>
    public string? FileExtension { get; set; }

    /// <summary>
    /// Schedule definition for when to execute file checks
    /// </summary>
    public required ScheduleDefinitionDto Schedule { get; set; }
}
