using FileRetrieval.Contracts.DTOs;

namespace RiskInsure.FileRetrieval.API.Models;

/// <summary>
/// T112: Request model for updating an existing file retrieval configuration.
/// </summary>
public class UpdateConfigurationRequest
{
    /// <summary>
    /// ETag for optimistic concurrency control (required)
    /// </summary>
    public required string ETag { get; init; }

    /// <summary>
    /// Human-readable configuration name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional description of configuration purpose
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Protocol type: FTP, HTTPS, AzureBlob
    /// </summary>
    public required string Protocol { get; init; }

    /// <summary>
    /// Protocol-specific settings (server, credentials, etc.)
    /// </summary>
    public required Dictionary<string, object> ProtocolSettings { get; init; }

    /// <summary>
    /// File path pattern with optional date tokens
    /// </summary>
    public required string FilePathPattern { get; init; }

    /// <summary>
    /// Filename pattern with optional date tokens
    /// </summary>
    public required string FilenamePattern { get; init; }

    /// <summary>
    /// Optional file extension filter
    /// </summary>
    public string? FileExtension { get; init; }

    /// <summary>
    /// Schedule definition (cron expression and timezone)
    /// </summary>
    public required ScheduleDefinitionDto Schedule { get; init; }

    /// <summary>
    /// Events to publish when files are discovered
    /// </summary>
    public required List<EventDefinitionDto> EventsToPublish { get; init; }

    /// <summary>
    /// Optional commands to send when files are discovered
    /// </summary>
    public List<CommandDefinitionDto>? CommandsToSend { get; init; }
}
