using Microsoft.Extensions.Logging;
using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Repositories;

namespace RiskInsure.FileRetrieval.Application.Services;

/// <summary>
/// Service for managing FileRetrievalConfiguration entities.
/// Provides business logic layer between message handlers/controllers and repositories.
/// </summary>
public class ConfigurationService
{
    private readonly IFileRetrievalConfigurationRepository _repository;
    private readonly ILogger<ConfigurationService> _logger;

    public ConfigurationService(
        IFileRetrievalConfigurationRepository repository,
        ILogger<ConfigurationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new FileRetrievalConfiguration.
    /// </summary>
    /// <param name="configuration">Configuration to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created configuration with ETag</returns>
    /// <exception cref="ArgumentException">If validation fails</exception>
    public async Task<FileRetrievalConfiguration> CreateAsync(
        FileRetrievalConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating configuration {ConfigurationId} for client {ClientId}",
            configuration.Id,
            configuration.ClientId);

        // Validate business rules
        configuration.Validate();

        // Set audit fields if not already set
        if (configuration.CreatedAt == default)
        {
            configuration.CreatedAt = DateTimeOffset.UtcNow;
        }

        // Ensure IsActive defaults to true
        if (!configuration.IsActive)
        {
            configuration.IsActive = true;
        }

        try
        {
            var created = await _repository.CreateAsync(configuration, cancellationToken);

            _logger.LogInformation(
                "Successfully created configuration {ConfigurationId} for client {ClientId}",
                created.Id,
                created.ClientId);

            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create configuration {ConfigurationId} for client {ClientId}",
                configuration.Id,
                configuration.ClientId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a configuration by ID for a specific client.
    /// </summary>
    /// <param name="clientId">Client ID for multi-tenant isolation</param>
    /// <param name="configurationId">Configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Configuration if found, null otherwise</returns>
    public async Task<FileRetrievalConfiguration?> GetByIdAsync(
        string clientId,
        Guid configurationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving configuration {ConfigurationId} for client {ClientId}",
            configurationId,
            clientId);

        try
        {
            var configuration = await _repository.GetByIdAsync(
                clientId,
                configurationId,
                cancellationToken);

            if (configuration == null)
            {
                _logger.LogWarning(
                    "Configuration {ConfigurationId} not found for client {ClientId}",
                    configurationId,
                    clientId);
            }

            return configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving configuration {ConfigurationId} for client {ClientId}",
                configurationId,
                clientId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all configurations for a specific client.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="includeInactive">Whether to include inactive configurations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of configurations</returns>
    public async Task<IReadOnlyList<FileRetrievalConfiguration>> GetByClientAsync(
        string clientId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving configurations for client {ClientId} (includeInactive: {IncludeInactive})",
            clientId,
            includeInactive);

        try
        {
            var configurations = await _repository.GetByClientAsync(
                clientId,
                cancellationToken);

            // Filter by active status if requested
            if (!includeInactive)
            {
                configurations = configurations
                    .Where(c => c.IsActive)
                    .ToList()
                    .AsReadOnly();
            }

            _logger.LogInformation(
                "Retrieved {Count} configurations for client {ClientId}",
                configurations.Count,
                clientId);

            return configurations;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving configurations for client {ClientId}",
                clientId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves configurations for a client with pagination and filtering.
    /// Supports clients with 20+ configurations (US4 requirement).
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="pageSize">Number of items per page (default 20)</param>
    /// <param name="continuationToken">Token for next page</param>
    /// <param name="protocolFilter">Optional protocol filter</param>
    /// <param name="isActiveFilter">Optional active status filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of configurations and continuation token</returns>
    public async Task<(IReadOnlyList<FileRetrievalConfiguration> Configurations, string? ContinuationToken)> GetByClientWithPaginationAsync(
        string clientId,
        int pageSize = 20,
        string? continuationToken = null,
        Domain.Enums.ProtocolType? protocolFilter = null,
        bool? isActiveFilter = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving paginated configurations for client {ClientId} (pageSize: {PageSize}, protocol: {Protocol}, isActive: {IsActive})",
            clientId,
            pageSize,
            protocolFilter,
            isActiveFilter);

        try
        {
            var result = await _repository.GetByClientWithPaginationAsync(
                clientId,
                pageSize,
                continuationToken,
                protocolFilter,
                isActiveFilter,
                cancellationToken);

            _logger.LogInformation(
                "Retrieved {Count} configurations for client {ClientId} with pagination (hasMore: {HasMore})",
                result.Configurations.Count,
                clientId,
                !string.IsNullOrEmpty(result.ContinuationToken));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving paginated configurations for client {ClientId}",
                clientId);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing configuration.
    /// </summary>
    /// <param name="configuration">Configuration with updated values</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated configuration</returns>
    /// <exception cref="ArgumentException">If validation fails</exception>
    public async Task<FileRetrievalConfiguration> UpdateAsync(
        FileRetrievalConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating configuration {ConfigurationId} for client {ClientId}",
            configuration.Id,
            configuration.ClientId);

        // Validate business rules
        configuration.Validate();

        // Update LastModifiedAt
        configuration.LastModifiedAt = DateTimeOffset.UtcNow;

        try
        {
            var updated = await _repository.UpdateAsync(configuration, cancellationToken);

            _logger.LogInformation(
                "Successfully updated configuration {ConfigurationId} for client {ClientId}",
                updated.Id,
                updated.ClientId);

            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update configuration {ConfigurationId} for client {ClientId}",
                configuration.Id,
                configuration.ClientId);
            throw;
        }
    }

    /// <summary>
    /// Deletes a configuration by marking it as inactive.
    /// Physical deletion is not supported to maintain audit trail.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="configurationId">Configuration ID</param>
    /// <param name="deletedBy">User performing deletion</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    public async Task<bool> DeleteAsync(
        string clientId,
        Guid configurationId,
        string deletedBy,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Soft-deleting configuration {ConfigurationId} for client {ClientId} by {DeletedBy}",
            configurationId,
            clientId,
            deletedBy);

        try
        {
            var configuration = await _repository.GetByIdAsync(
                clientId,
                configurationId,
                cancellationToken);

            if (configuration == null)
            {
                _logger.LogWarning(
                    "Configuration {ConfigurationId} not found for client {ClientId} - cannot delete",
                    configurationId,
                    clientId);
                return false;
            }

            // Soft delete by marking as inactive
            configuration.IsActive = false;
            configuration.LastModifiedAt = DateTimeOffset.UtcNow;
            configuration.LastModifiedBy = deletedBy;

            await _repository.UpdateAsync(configuration, cancellationToken);

            _logger.LogInformation(
                "Successfully soft-deleted configuration {ConfigurationId} for client {ClientId}",
                configurationId,
                clientId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deleting configuration {ConfigurationId} for client {ClientId}",
                configurationId,
                clientId);
            throw;
        }
    }
}
