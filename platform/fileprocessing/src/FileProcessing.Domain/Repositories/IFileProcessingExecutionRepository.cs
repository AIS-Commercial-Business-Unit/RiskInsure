namespace RiskInsure.FileProcessing.Domain.Repositories;

/// <summary>
/// T032: Repository interface for FileProcessingExecution entity.
/// Provides append-only execution history with client-scoped access.
/// </summary>
public interface IFileProcessingExecutionRepository
{
    /// <summary>
    /// Create a new execution record
    /// </summary>
    Task<Entities.FileProcessingExecution> CreateAsync(
        Entities.FileProcessingExecution execution,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get execution by ID (client-scoped)
    /// </summary>
    Task<Entities.FileProcessingExecution?> GetByIdAsync(
        string clientId,
        Guid configurationId,
        Guid executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get execution history for a configuration
    /// </summary>
    Task<IReadOnlyList<Entities.FileProcessingExecution>> GetByConfigurationAsync(
        string clientId,
        Guid configurationId,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get execution history with date range filtering
    /// </summary>
    Task<IReadOnlyList<Entities.FileProcessingExecution>> GetByConfigurationAndDateRangeAsync(
        string clientId,
        Guid configurationId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get execution history with pagination, filtering by status and date range (T120, T123)
    /// </summary>
    Task<(IReadOnlyList<Entities.FileProcessingExecution> Executions, string? ContinuationToken)> GetExecutionHistoryAsync(
        string clientId,
        Guid configurationId,
        int pageSize = 50,
        string? continuationToken = null,
        Enums.ExecutionStatus? statusFilter = null,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update execution status (for state transitions)
    /// </summary>
    Task<Entities.FileProcessingExecution> UpdateAsync(
        Entities.FileProcessingExecution execution,
        CancellationToken cancellationToken = default);
}
