namespace RiskInsure.FileRetrieval.Domain.Enums;

/// <summary>
/// Execution status for file retrieval execution attempts.
/// T019: ExecutionStatus enum
/// </summary>
public enum ExecutionStatus
{
    /// <summary>Execution is pending (not started yet)</summary>
    Pending = 1,
    
    /// <summary>Execution is currently in progress</summary>
    InProgress = 2,
    
    /// <summary>Execution completed successfully</summary>
    Completed = 3,
    
    /// <summary>Execution failed with errors</summary>
    Failed = 4
}
