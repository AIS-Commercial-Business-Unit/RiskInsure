using RiskInsure.FileRetrieval.Domain.Enums;

namespace RiskInsure.FileRetrieval.Domain.Entities;

/// <summary>
/// Represents a single execution attempt of a FileRetrievalConfiguration, 
/// tracking success/failure and discovered files.
/// </summary>
public class FileRetrievalExecution
{
    /// <summary>
    /// Unique execution identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Client owning this execution
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    /// Configuration that was executed
    /// </summary>
    public required Guid ConfigurationId { get; set; }

    /// <summary>
    /// When execution started
    /// </summary>
    public required DateTimeOffset ExecutionStartedAt { get; set; }

    /// <summary>
    /// When execution finished (null if still in progress)
    /// </summary>
    public DateTimeOffset? ExecutionCompletedAt { get; set; }

    /// <summary>
    /// Execution status (Pending, InProgress, Completed, Failed)
    /// </summary>
    public required ExecutionStatus Status { get; set; }

    /// <summary>
    /// Number of files discovered
    /// </summary>
    public int FilesFound { get; set; }

    /// <summary>
    /// Number of files for which events were published
    /// </summary>
    public int FilesProcessed { get; set; }

    /// <summary>
    /// Error details if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error category (AuthenticationFailure, ConnectionTimeout, etc.)
    /// </summary>
    public string? ErrorCategory { get; set; }

    /// <summary>
    /// Path pattern after token replacement
    /// </summary>
    public required string ResolvedFilePathPattern { get; set; }

    /// <summary>
    /// Filename pattern after token replacement
    /// </summary>
    public required string ResolvedFilenamePattern { get; set; }

    /// <summary>
    /// Execution duration in milliseconds
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Number of retry attempts (max 3)
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Document type discriminator for Cosmos DB
    /// </summary>
    public string Type { get; set; } = nameof(FileRetrievalExecution);

    /// <summary>
    /// ETag for optimistic concurrency control in Cosmos DB
    /// </summary>
    public required string ETag { get; set; }

    /// <summary>
    /// Validates the execution business rules
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
            throw new ArgumentException("ClientId must not be empty", nameof(ClientId));

        if (ConfigurationId == Guid.Empty)
            throw new ArgumentException("ConfigurationId must not be empty", nameof(ConfigurationId));

        if (ExecutionStartedAt > DateTimeOffset.UtcNow)
            throw new ArgumentException("ExecutionStartedAt cannot be in the future", nameof(ExecutionStartedAt));

        if (ExecutionCompletedAt.HasValue && ExecutionCompletedAt < ExecutionStartedAt)
            throw new ArgumentException("ExecutionCompletedAt cannot be before ExecutionStartedAt", nameof(ExecutionCompletedAt));

        if (FilesFound < 0)
            throw new ArgumentException("FilesFound cannot be negative", nameof(FilesFound));

        if (FilesProcessed < 0 || FilesProcessed > FilesFound)
            throw new ArgumentException("FilesProcessed must be between 0 and FilesFound", nameof(FilesProcessed));

        if (ErrorMessage?.Length > 5000)
            throw new ArgumentException("ErrorMessage max 5000 characters", nameof(ErrorMessage));

        if (ErrorCategory?.Length > 100)
            throw new ArgumentException("ErrorCategory max 100 characters", nameof(ErrorCategory));

        if (string.IsNullOrWhiteSpace(ResolvedFilePathPattern))
            throw new ArgumentException("ResolvedFilePathPattern must not be empty", nameof(ResolvedFilePathPattern));

        if (string.IsNullOrWhiteSpace(ResolvedFilenamePattern))
            throw new ArgumentException("ResolvedFilenamePattern must not be empty", nameof(ResolvedFilenamePattern));

        if (DurationMs < 0)
            throw new ArgumentException("DurationMs cannot be negative", nameof(DurationMs));

        if (RetryCount < 0 || RetryCount > 3)
            throw new ArgumentException("RetryCount must be between 0 and 3", nameof(RetryCount));

        // Terminal state validation
        if (Status is ExecutionStatus.Completed or ExecutionStatus.Failed)
        {
            if (!ExecutionCompletedAt.HasValue)
                throw new ArgumentException("ExecutionCompletedAt required when Status is Completed or Failed", nameof(ExecutionCompletedAt));
        }

        if (Status == ExecutionStatus.Failed && string.IsNullOrWhiteSpace(ErrorMessage))
            throw new ArgumentException("ErrorMessage required when Status is Failed", nameof(ErrorMessage));
    }
}
