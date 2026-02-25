namespace RiskInsure.FileRetrieval.Domain.Repositories;

/// <summary>
/// T033: Repository interface for DiscoveredFile entity.
/// Provides file discovery tracking with idempotency enforcement.
/// </summary>
public interface IDiscoveredFileRepository
{
    /// <summary>
    /// Create a new discovered file record.
    /// Returns null if file already discovered (idempotency via unique key constraint).
    /// </summary>
    Task<Entities.DiscoveredFile?> CreateAsync(
        Entities.DiscoveredFile discoveredFile,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if file was already discovered today (idempotency check)
    /// </summary>
    Task<bool> ExistsAsync(
        string clientId,
        Guid configurationId,
        string fileUrl,
        DateOnly discoveryDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all discovered files for an execution
    /// </summary>
    Task<IReadOnlyList<Entities.DiscoveredFile>> GetByExecutionAsync(
        string clientId,
        Guid configurationId,
        Guid executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all discovered files for a configuration
    /// </summary>
    Task<IReadOnlyList<Entities.DiscoveredFile>> GetByConfigurationAsync(
        string clientId,
        Guid configurationId,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update discovered file status (for event publishing tracking)
    /// </summary>
    Task<Entities.DiscoveredFile> UpdateAsync(
        Entities.DiscoveredFile discoveredFile,
        CancellationToken cancellationToken = default);
}
