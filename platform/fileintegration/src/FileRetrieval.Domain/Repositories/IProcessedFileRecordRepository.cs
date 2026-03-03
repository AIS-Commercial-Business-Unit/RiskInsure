namespace RiskInsure.FileRetrieval.Domain.Repositories;

/// <summary>
/// Repository for persisted file processing records.
/// </summary>
public interface IProcessedFileRecordRepository
{
    /// <summary>
    /// Creates a processing record. Returns null when an idempotent duplicate already exists.
    /// </summary>
    Task<Entities.ProcessedFileRecord?> CreateAsync(
        Entities.ProcessedFileRecord record,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets processed records for a configuration ordered by most recent first.
    /// </summary>
    Task<IReadOnlyList<Entities.ProcessedFileRecord>> GetByConfigurationAsync(
        string clientId,
        Guid configurationId,
        int pageSize = 50,
        string? fileName = null,
        Guid? executionId = null,
        CancellationToken cancellationToken = default);
}
