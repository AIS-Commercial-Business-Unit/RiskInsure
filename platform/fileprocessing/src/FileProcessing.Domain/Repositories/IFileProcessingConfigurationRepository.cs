namespace RiskInsure.FileProcessing.Domain.Repositories;

/// <summary>
/// T031: Repository interface for FileProcessingConfiguration entity.
/// Provides CRUD operations with client-scoped access and optimistic concurrency.
/// </summary>
public interface IFileProcessingConfigurationRepository
{
    /// <summary>
    /// Create a new file processing configuration
    /// </summary>
    Task<Entities.FileProcessingConfiguration> CreateAsync(
        Entities.FileProcessingConfiguration configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get configuration by ID (client-scoped)
    /// </summary>
    Task<Entities.FileProcessingConfiguration?> GetByIdAsync(
        string clientId,
        Guid configurationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all configurations for a client
    /// </summary>
    Task<IReadOnlyList<Entities.FileProcessingConfiguration>> GetByClientAsync(
        string clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get configurations for a client with pagination and filtering
    /// </summary>
    Task<(IReadOnlyList<Entities.FileProcessingConfiguration> Configurations, string? ContinuationToken)> GetByClientWithPaginationAsync(
        string clientId,
        int pageSize = 20,
        string? continuationToken = null,
        Enums.ProtocolType? protocolFilter = null,
        bool? isActiveFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active configurations for a client
    /// </summary>
    Task<IReadOnlyList<Entities.FileProcessingConfiguration>> GetActiveByClientAsync(
        string clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active configurations across all clients (for scheduler)
    /// </summary>
    Task<IReadOnlyList<Entities.FileProcessingConfiguration>> GetAllActiveConfigurationsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update configuration with optimistic concurrency (ETag check)
    /// </summary>
    Task<Entities.FileProcessingConfiguration> UpdateAsync(
        Entities.FileProcessingConfiguration configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete configuration (set IsActive = false)
    /// </summary>
    Task DeleteAsync(
        string clientId,
        Guid configurationId,
        string etag,
        CancellationToken cancellationToken = default);
}
