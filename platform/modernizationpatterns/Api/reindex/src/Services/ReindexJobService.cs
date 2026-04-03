namespace RiskInsure.Modernization.Reindex.Services;

using System.Collections.Concurrent;

/// <summary>
/// Manages background reindex jobs with status tracking.
/// Allows the reindex endpoint to return immediately (202 Accepted)
/// while processing happens in the background.
/// </summary>
public interface IReindexJobService
{
    /// <summary>
    /// Create a new job and return its ID.
    /// </summary>
    string CreateJob(bool clean, string? pattern);

    /// <summary>
    /// Get the status of a job by ID.
    /// </summary>
    ReindexJobStatus? GetJobStatus(string jobId);

    /// <summary>
    /// Update job status during execution.
    /// </summary>
    void UpdateJobStatus(string jobId, ReindexJobStatus status);

    /// <summary>
    /// Complete a job with results.
    /// </summary>
    void CompleteJob(string jobId, ReindexJobResult result);

    /// <summary>
    /// Fail a job with an error.
    /// </summary>
    void FailJob(string jobId, string error, string? message = null);
}

public record ReindexJobStatus(
    string JobId,
    string State, // "pending", "running", "completed", "failed"
    int Progress, // 0-100
    string? CurrentStep,
    DateTime StartedUtc,
    DateTime? CompletedUtc,
    ReindexJobResult? Result,
    string? Error,
    string? ErrorMessage
);

public record ReindexJobResult(
    int PatternsProcessed,
    int InboxDocumentsProcessed,
    int AgenticDocumentsProcessed,
    int ChunksCreated,
    int DocumentsUploaded,
    int DocumentsDeleted,
    int TotalDocumentsInIndex,
    double ElapsedSeconds,
    List<string> Patterns,
    List<string> InboxDocuments,
    List<string> AgenticDocuments
);

public class ReindexJobService : IReindexJobService
{
    private readonly ConcurrentDictionary<string, ReindexJobStatus> _jobs = new();

    public string CreateJob(bool clean, string? pattern)
    {
        var jobId = Guid.NewGuid().ToString("N").Substring(0, 12);
        var status = new ReindexJobStatus(
            JobId: jobId,
            State: "pending",
            Progress: 0,
            CurrentStep: "Waiting to start",
            StartedUtc: DateTime.UtcNow,
            CompletedUtc: null,
            Result: null,
            Error: null,
            ErrorMessage: null
        );

        _jobs[jobId] = status;
        return jobId;
    }

    public ReindexJobStatus? GetJobStatus(string jobId)
    {
        _jobs.TryGetValue(jobId, out var status);
        return status;
    }

    public void UpdateJobStatus(string jobId, ReindexJobStatus status)
    {
        _jobs[jobId] = status;
    }

    public void CompleteJob(string jobId, ReindexJobResult result)
    {
        if (_jobs.TryGetValue(jobId, out var current))
        {
            var completed = current with
            {
                State = "completed",
                Progress = 100,
                Result = result,
                CompletedUtc = DateTime.UtcNow,
                CurrentStep = "Complete"
            };

            _jobs[jobId] = completed;
        }
    }

    public void FailJob(string jobId, string error, string? message = null)
    {
        if (_jobs.TryGetValue(jobId, out var current))
        {
            var failed = current with
            {
                State = "failed",
                Error = error,
                ErrorMessage = message,
                CompletedUtc = DateTime.UtcNow,
                CurrentStep = "Failed"
            };

            _jobs[jobId] = failed;
        }
    }
}
