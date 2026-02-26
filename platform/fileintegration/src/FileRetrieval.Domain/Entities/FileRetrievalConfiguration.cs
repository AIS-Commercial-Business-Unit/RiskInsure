using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.ValueObjects;

namespace RiskInsure.FileRetrieval.Domain.Entities;

/// <summary>
/// Represents a configured file check for a client, defining WHERE to look, WHEN to look, 
/// and WHAT TO DO when files are found.
/// </summary>
public class FileRetrievalConfiguration
{
    /// <summary>
    /// Unique configuration identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Client owning this configuration
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    /// Human-readable configuration name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of purpose
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Protocol type (FTP, HTTPS, AzureBlob)
    /// </summary>
    public required ProtocolType Protocol { get; set; }

    /// <summary>
    /// Protocol-specific connection settings
    /// </summary>
    public required ProtocolSettings ProtocolSettings { get; set; }

    /// <summary>
    /// Path pattern with optional tokens (e.g., "/data/{yyyy}/{mm}")
    /// </summary>
    public required string FilePathPattern { get; set; }

    /// <summary>
    /// Filename pattern with optional tokens (e.g., "report_{yyyy}{mm}{dd}.*")
    /// </summary>
    public required string FilenamePattern { get; set; }

    /// <summary>
    /// File extension filter (e.g., "xlsx", "pdf")
    /// </summary>
    public string? FileExtension { get; set; }

    /// <summary>
    /// When to execute file checks (schedule definition)
    /// </summary>
    public required ScheduleDefinition Schedule { get; set; }

    /// <summary>
    /// Whether configuration is active (inactive configs are not scheduled)
    /// </summary>
    public bool IsActive { get; set; } = true;

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
    /// Optimistic concurrency token (Cosmos DB managed)
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Document type discriminator for Cosmos DB
    /// </summary>
    public string Type { get; set; } = nameof(FileRetrievalConfiguration);

    /// <summary>
    /// Validates the configuration business rules
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId) || ClientId.Length > 50)
            throw new ArgumentException("ClientId must not be empty and max 50 characters", nameof(ClientId));

        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 200)
            throw new ArgumentException("Name must not be empty and max 200 characters", nameof(Name));

        if (Description?.Length > 1000)
            throw new ArgumentException("Description max 1000 characters", nameof(Description));

        if (string.IsNullOrWhiteSpace(FilePathPattern) || FilePathPattern.Length > 500)
            throw new ArgumentException("FilePathPattern must not be empty and max 500 characters", nameof(FilePathPattern));

        if (string.IsNullOrWhiteSpace(FilenamePattern) || FilenamePattern.Length > 200)
            throw new ArgumentException("FilenamePattern must not be empty and max 200 characters", nameof(FilenamePattern));

        if (FileExtension?.Length > 10)
            throw new ArgumentException("FileExtension max 10 characters", nameof(FileExtension));

        if (CreatedAt > DateTimeOffset.UtcNow)
            throw new ArgumentException("CreatedAt cannot be in the future", nameof(CreatedAt));

        if (LastModifiedAt.HasValue && LastModifiedAt < CreatedAt)
            throw new ArgumentException("LastModifiedAt cannot be before CreatedAt", nameof(LastModifiedAt));

        // Validate that protocol settings match the protocol type
        var expectedProtocolType = ProtocolSettings.ProtocolType;
        if (expectedProtocolType != Protocol)
            throw new ArgumentException($"ProtocolSettings type {expectedProtocolType} does not match Protocol {Protocol}");
    }
}
