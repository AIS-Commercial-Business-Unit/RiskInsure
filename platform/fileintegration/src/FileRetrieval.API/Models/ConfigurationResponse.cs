using FileRetrieval.Contracts.DTOs;

namespace RiskInsure.FileRetrieval.API.Models;

/// <summary>
/// Response model for file retrieval configuration.
/// </summary>
public class ConfigurationResponse
{
    /// <summary>
    /// Unique configuration identifier
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// Client owning this configuration
    /// </summary>
    public required string ClientId { get; set; }

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
    /// Protocol-specific connection settings (sanitized - secrets removed)
    /// </summary>
    public required Dictionary<string, object> ProtocolSettings { get; set; }

    /// <summary>
    /// Path pattern with optional date tokens
    /// </summary>
    public required string FilePathPattern { get; set; }

    /// <summary>
    /// Filename pattern with optional date tokens
    /// </summary>
    public required string FilenamePattern { get; set; }

    /// <summary>
    /// File extension filter
    /// </summary>
    public string? FileExtension { get; set; }

    /// <summary>
    /// Schedule definition
    /// </summary>
    public required ScheduleDefinitionDto Schedule { get; set; }

    /// <summary>
    /// Whether configuration is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// When configuration was created
    /// </summary>
    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// User who created configuration
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTimeOffset? LastModifiedAt { get; set; }

    /// <summary>
    /// User who last updated
    /// </summary>
    public string? LastModifiedBy { get; set; }

    /// <summary>
    /// Last successful execution timestamp
    /// </summary>
    public DateTimeOffset? LastExecutedAt { get; set; }

    /// <summary>
    /// Next planned execution (calculated from schedule)
    /// </summary>
    public DateTimeOffset? NextScheduledRun { get; set; }

    /// <summary>
    /// Optimistic concurrency token
    /// </summary>
    public string? ETag { get; set; }
}
