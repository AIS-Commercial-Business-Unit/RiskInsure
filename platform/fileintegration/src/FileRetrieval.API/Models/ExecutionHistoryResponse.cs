namespace RiskInsure.FileRetrieval.API.Models;

/// <summary>
/// T122: Response model for execution history listing.
/// </summary>
public class ExecutionHistoryResponse
{
    public required Guid Id { get; init; }
    public required Guid ConfigurationId { get; init; }
    public required string ConfigurationName { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset ExecutionStartedAt { get; init; }
    public DateTimeOffset? ExecutionCompletedAt { get; init; }
    public required long DurationMs { get; init; }
    public required int FilesFound { get; init; }
    public required int EventsPublished { get; init; }
    public required int CommandsSent { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCategory { get; init; }
}

/// <summary>
/// T126: Detailed execution response including discovered files.
/// </summary>
public class ExecutionDetailsResponse
{
    public required Guid Id { get; init; }
    public required Guid ConfigurationId { get; init; }
    public required string ConfigurationName { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset ExecutionStartedAt { get; init; }
    public DateTimeOffset? ExecutionCompletedAt { get; init; }
    public required long DurationMs { get; init; }
    public required int FilesFound { get; init; }
    public required int EventsPublished { get; init; }
    public required int CommandsSent { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCategory { get; init; }
    
    /// <summary>
    /// T126: List of discovered files with metadata
    /// </summary>
    public List<DiscoveredFileInfo>? DiscoveredFiles { get; init; }
}

/// <summary>
/// T126: Discovered file metadata
/// </summary>
public class DiscoveredFileInfo
{
    public required string FileName { get; init; }
    public required string FileUrl { get; init; }
    public long? FileSizeBytes { get; init; }
    public DateTimeOffset? FileLastModified { get; init; }
    public required DateTimeOffset DiscoveredAt { get; init; }
}

/// <summary>
/// Paginated execution history response
/// </summary>
public class PaginatedExecutionHistoryResponse
{
    public required List<ExecutionHistoryResponse> Executions { get; init; }
    public string? ContinuationToken { get; init; }
    public bool HasMore { get; init; }
    public int Count { get; init; }
}

/// <summary>
/// T128: Execution metrics aggregation response
/// </summary>
public class ExecutionMetricsResponse
{
    public required Guid ConfigurationId { get; init; }
    public required DateTimeOffset StartDate { get; init; }
    public required DateTimeOffset EndDate { get; init; }
    public required int TotalExecutions { get; init; }
    public required int SuccessfulExecutions { get; init; }
    public required int FailedExecutions { get; init; }
    public required double SuccessRate { get; init; }
    public required double AverageDurationSeconds { get; init; }
    public required int TotalFilesDiscovered { get; init; }
    public required double FilesDiscoveredPerDay { get; init; }
}
