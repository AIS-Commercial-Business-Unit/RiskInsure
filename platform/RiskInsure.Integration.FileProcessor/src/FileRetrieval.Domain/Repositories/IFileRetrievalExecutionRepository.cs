namespace RiskInsure.FileRetrieval.Domain.Repositories;

/// <summary>
/// T032: Repository interface for FileRetrievalExecution entity.
/// Provides append-only execution history with client-scoped access.
/// </summary>
public interface IFileRetrievalExecutionRepository
{
    /// <summary>
    /// Create a new execution record
    /// </summary>
    Task<Entities.FileRetrievalExecution> CreateAsync(
        Entities.FileRetrievalExecution execution,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get execution by ID (client-scoped)
    /// </summary>
    Task<Entities.FileRetrievalExecution?> GetByIdAsync(
        string clientId,
        Guid configurationId,
        Guid executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get execution history for a configuration
    /// </summary>
    Task<IReadOnlyList<Entities.FileRetrievalExecution>> GetByConfigurationAsync(
        string clientId,
        Guid configurationId,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get execution history with date range filtering
    /// </summary>
    Task<IReadOnlyList<Entities.FileRetrievalExecution>> GetByConfigurationAndDateRangeAsync(
        string clientId,
        Guid configurationId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get execution history with pagination, filtering by status and date range (T120, T123)
    /// </summary>
    Task<(IReadOnlyList<Entities.FileRetrievalExecution> Executions, string? ContinuationToken)> GetExecutionHistoryAsync(
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
    Task<Entities.FileRetrievalExecution> UpdateAsync(
        Entities.FileRetrievalExecution execution,
        CancellationToken cancellationToken = default);
}
