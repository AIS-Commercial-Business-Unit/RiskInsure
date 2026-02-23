using RiskInsure.FileRetrieval.Domain.Enums;

namespace RiskInsure.FileRetrieval.Domain.Entities;

/// <summary>
/// Represents a file found during a retrieval check, used for idempotency tracking 
/// to prevent duplicate workflow triggers.
/// </summary>
public class DiscoveredFile
{
    /// <summary>
    /// Unique discovered file identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Client owning this file
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    /// Configuration that discovered this file
    /// </summary>
    public required Guid ConfigurationId { get; set; }

    /// <summary>
    /// Execution that discovered this file
    /// </summary>
    public required Guid ExecutionId { get; set; }

    /// <summary>
    /// Full file location/URL
    /// </summary>
    public required string FileUrl { get; set; }

    /// <summary>
    /// File name only
    /// </summary>
    public required string Filename { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// File last modified timestamp
    /// </summary>
    public DateTimeOffset? LastModified { get; set; }

    /// <summary>
    /// When file was discovered
    /// </summary>
    public required DateTimeOffset DiscoveredAt { get; set; }

    /// <summary>
    /// Discovery date (for idempotency - same file on same day = single record)
    /// </summary>
    public required DateOnly DiscoveryDate { get; set; }

    /// <summary>
    /// Discovery status (Pending, EventPublished, Failed)
    /// </summary>
    public required DiscoveryStatus Status { get; set; }

    /// <summary>
    /// When FileDiscovered event was published
    /// </summary>
    public DateTimeOffset? EventPublishedAt { get; set; }

    /// <summary>
    /// Error if event publish failed
    /// </summary>
    public string? ProcessingError { get; set; }

    /// <summary>
    /// Document type discriminator for Cosmos DB
    /// </summary>
    public string Type { get; set; } = nameof(DiscoveredFile);

    /// <summary>
    /// Validates the discovered file business rules
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
            throw new ArgumentException("ClientId must not be empty", nameof(ClientId));

        if (ConfigurationId == Guid.Empty)
            throw new ArgumentException("ConfigurationId must not be empty", nameof(ConfigurationId));

        if (ExecutionId == Guid.Empty)
            throw new ArgumentException("ExecutionId must not be empty", nameof(ExecutionId));

        if (string.IsNullOrWhiteSpace(FileUrl) || FileUrl.Length > 2000)
            throw new ArgumentException("FileUrl must not be empty and max 2000 characters", nameof(FileUrl));

        if (string.IsNullOrWhiteSpace(Filename) || Filename.Length > 255)
            throw new ArgumentException("Filename must not be empty and max 255 characters", nameof(Filename));

        if (FileSize.HasValue && FileSize < 0)
            throw new ArgumentException("FileSize cannot be negative", nameof(FileSize));

        if (DiscoveredAt > DateTimeOffset.UtcNow)
            throw new ArgumentException("DiscoveredAt cannot be in the future", nameof(DiscoveredAt));

        if (DiscoveryDate > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("DiscoveryDate cannot be in the future", nameof(DiscoveryDate));

        if (ProcessingError?.Length > 2000)
            throw new ArgumentException("ProcessingError max 2000 characters", nameof(ProcessingError));

        // Terminal state validation
        if (Status == DiscoveryStatus.EventPublished && !EventPublishedAt.HasValue)
            throw new ArgumentException("EventPublishedAt required when Status is EventPublished", nameof(EventPublishedAt));

        if (Status == DiscoveryStatus.Failed && string.IsNullOrWhiteSpace(ProcessingError))
            throw new ArgumentException("ProcessingError required when Status is Failed", nameof(ProcessingError));
    }
}
