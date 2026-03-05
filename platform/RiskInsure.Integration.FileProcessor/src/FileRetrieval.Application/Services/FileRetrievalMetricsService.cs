using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace RiskInsure.FileRetrieval.Application.Services;

/// <summary>
/// T102: Service for collecting and exposing metrics about file retrieval operations.
/// Tracks active configurations per client, execution counts per protocol, and other operational metrics.
/// </summary>
public class FileRetrievalMetricsService
{
    private readonly ILogger<FileRetrievalMetricsService> _logger;
    private readonly Meter _meter;
    
    // Counters
    private readonly Counter<long> _configurationsCreatedCounter;
    private readonly Counter<long> _configurationsDeletedCounter;
    private readonly Counter<long> _fileChecksExecutedCounter;
    private readonly Counter<long> _filesDiscoveredCounter;
    private readonly Counter<long> _executionFailuresCounter;
    
    // Observable Gauges (updated periodically)
    private readonly ConcurrentDictionary<string, int> _activeConfigsByClient;
    private readonly ConcurrentDictionary<string, long> _executionCountsByProtocol;
    private readonly ConcurrentDictionary<string, long> _failureCountsByProtocol;
    
    // Histogram
    private readonly Histogram<double> _fileCheckDurationHistogram;

    public FileRetrievalMetricsService(ILogger<FileRetrievalMetricsService> logger)
    {
        _logger = logger;
        _meter = new Meter("RiskInsure.FileRetrieval", "1.0.0");
        
        // Initialize collections
        _activeConfigsByClient = new ConcurrentDictionary<string, int>();
        _executionCountsByProtocol = new ConcurrentDictionary<string, long>();
        _failureCountsByProtocol = new ConcurrentDictionary<string, long>();
        
        // Create counters
        _configurationsCreatedCounter = _meter.CreateCounter<long>(
            "file_retrieval_configurations_created_total",
            description: "Total number of configurations created");
        
        _configurationsDeletedCounter = _meter.CreateCounter<long>(
            "file_retrieval_configurations_deleted_total",
            description: "Total number of configurations deleted");
        
        _fileChecksExecutedCounter = _meter.CreateCounter<long>(
            "file_retrieval_checks_executed_total",
            description: "Total number of file checks executed");
        
        _filesDiscoveredCounter = _meter.CreateCounter<long>(
            "file_retrieval_files_discovered_total",
            description: "Total number of files discovered");
        
        _executionFailuresCounter = _meter.CreateCounter<long>(
            "file_retrieval_execution_failures_total",
            description: "Total number of execution failures");
        
        // Create histogram for durations
        _fileCheckDurationHistogram = _meter.CreateHistogram<double>(
            "file_retrieval_check_duration_seconds",
            unit: "s",
            description: "Duration of file check operations in seconds");
        
        // Create observable gauges
        _meter.CreateObservableGauge(
            "file_retrieval_active_configurations_by_client",
            () => _activeConfigsByClient.Select(kvp => new Measurement<int>(kvp.Value, new KeyValuePair<string, object?>("client_id", kvp.Key))),
            description: "Number of active configurations per client");
        
        _meter.CreateObservableGauge(
            "file_retrieval_executions_by_protocol",
            () => _executionCountsByProtocol.Select(kvp => new Measurement<long>(kvp.Value, new KeyValuePair<string, object?>("protocol", kvp.Key))),
            description: "Total number of executions by protocol type");
        
        _meter.CreateObservableGauge(
            "file_retrieval_failures_by_protocol",
            () => _failureCountsByProtocol.Select(kvp => new Measurement<long>(kvp.Value, new KeyValuePair<string, object?>("protocol", kvp.Key))),
            description: "Total number of failures by protocol type");
    }

    /// <summary>
    /// Records a configuration creation event.
    /// </summary>
    public void RecordConfigurationCreated(string clientId, string protocol)
    {
        _configurationsCreatedCounter.Add(1, 
            new KeyValuePair<string, object?>("client_id", clientId),
            new KeyValuePair<string, object?>("protocol", protocol));
        
        _logger.LogDebug("Recorded configuration creation: Client={ClientId}, Protocol={Protocol}", clientId, protocol);
    }

    /// <summary>
    /// Records a configuration deletion event.
    /// </summary>
    public void RecordConfigurationDeleted(string clientId, string protocol)
    {
        _configurationsDeletedCounter.Add(1,
            new KeyValuePair<string, object?>("client_id", clientId),
            new KeyValuePair<string, object?>("protocol", protocol));
        
        _logger.LogDebug("Recorded configuration deletion: Client={ClientId}, Protocol={Protocol}", clientId, protocol);
    }

    /// <summary>
    /// Records a file check execution.
    /// </summary>
    public void RecordFileCheckExecuted(string clientId, string protocol, double durationSeconds, bool success)
    {
        _fileChecksExecutedCounter.Add(1,
            new KeyValuePair<string, object?>("client_id", clientId),
            new KeyValuePair<string, object?>("protocol", protocol),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
        
        _fileCheckDurationHistogram.Record(durationSeconds,
            new KeyValuePair<string, object?>("protocol", protocol),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
        
        // Update protocol-specific counters
        _executionCountsByProtocol.AddOrUpdate(protocol, 1, (_, count) => count + 1);
        
        if (!success)
        {
            _executionFailuresCounter.Add(1,
                new KeyValuePair<string, object?>("protocol", protocol));
            _failureCountsByProtocol.AddOrUpdate(protocol, 1, (_, count) => count + 1);
        }
        
        _logger.LogDebug(
            "Recorded file check: Client={ClientId}, Protocol={Protocol}, Duration={Duration}s, Success={Success}",
            clientId, protocol, durationSeconds, success);
    }

    /// <summary>
    /// Records files discovered during a check.
    /// </summary>
    public void RecordFilesDiscovered(string clientId, string protocol, int fileCount)
    {
        _filesDiscoveredCounter.Add(fileCount,
            new KeyValuePair<string, object?>("client_id", clientId),
            new KeyValuePair<string, object?>("protocol", protocol));
        
        _logger.LogDebug(
            "Recorded files discovered: Client={ClientId}, Protocol={Protocol}, Count={Count}",
            clientId, protocol, fileCount);
    }

    /// <summary>
    /// Updates active configuration count for a client.
    /// Should be called when configurations are created, deleted, or activated/deactivated.
    /// </summary>
    public void UpdateActiveConfigurationCount(string clientId, int activeCount)
    {
        _activeConfigsByClient.AddOrUpdate(clientId, activeCount, (_, _) => activeCount);
        
        _logger.LogDebug("Updated active configuration count: Client={ClientId}, Count={Count}", clientId, activeCount);
    }

    /// <summary>
    /// Gets current metrics snapshot for reporting.
    /// </summary>
    public MetricsSnapshot GetSnapshot()
    {
        return new MetricsSnapshot
        {
            ActiveConfigurationsByClient = _activeConfigsByClient.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ExecutionCountsByProtocol = _executionCountsByProtocol.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            FailureCountsByProtocol = _failureCountsByProtocol.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }
}

/// <summary>
/// Snapshot of current metrics state.
/// </summary>
public record MetricsSnapshot
{
    public required Dictionary<string, int> ActiveConfigurationsByClient { get; init; }
    public required Dictionary<string, long> ExecutionCountsByProtocol { get; init; }
    public required Dictionary<string, long> FailureCountsByProtocol { get; init; }
}
