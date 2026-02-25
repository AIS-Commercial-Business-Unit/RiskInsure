# Monitoring Dashboard: File Retrieval Service

**T131**: This document provides queries and guidance for monitoring the File Retrieval Service using Application Insights and Azure Monitor.

## Overview

The File Retrieval Service exposes metrics and logs through:
- **Application Insights**: Custom metrics, traces, and exceptions
- **Azure Monitor**: Performance counters and availability
- **REST API**: Execution history and metrics endpoints

## Key Metrics to Monitor

### 1. File Check Success Rate

**Query** (Application Insights - Kusto):
```kql
customMetrics
| where name == "file_retrieval_checks_executed_total"
| extend status = tostring(customDimensions.status)
| summarize 
    Total = count(),
    Success = countif(status == "success"),
    Failed = countif(status == "failure")
    by bin(timestamp, 1h)
| project timestamp, SuccessRate = (Success * 100.0) / Total, Total, Success, Failed
| render timechart
```

**Alert Threshold**: Success rate < 90% over 1 hour

### 2. File Check Duration

**Query**:
```kql
customMetrics
| where name == "file_retrieval_check_duration_seconds"
| extend protocol = tostring(customDimensions.protocol)
| summarize 
    AvgDuration = avg(value),
    P50 = percentile(value, 50),
    P95 = percentile(value, 95),
    P99 = percentile(value, 99)
    by protocol, bin(timestamp, 5m)
| render timechart
```

**Alert Threshold**: P95 duration > 30 seconds

### 3. Files Discovered

**Query**:
```kql
customMetrics
| where name == "file_retrieval_files_discovered_total"
| extend protocol = tostring(customDimensions.protocol)
| extend client_id = tostring(customDimensions.client_id)
| summarize FilesDiscovered = sum(value) by bin(timestamp, 1h), protocol
| render timechart
```

### 4. Active Configurations per Client

**Query**:
```kql
customMetrics
| where name == "file_retrieval_active_configurations_by_client"
| extend client_id = tostring(customDimensions.client_id)
| summarize ActiveConfigs = max(value) by client_id, bin(timestamp, 1h)
| render barchart
```

### 5. Execution Failures by Protocol

**Query**:
```kql
customMetrics
| where name == "file_retrieval_execution_failures_total"
| extend protocol = tostring(customDimensions.protocol)
| summarize Failures = sum(value) by protocol, bin(timestamp, 1h)
| render columnchart
```

**Alert Threshold**: > 10 failures per hour for any protocol

### 6. Protocol Errors by Category

**Query**:
```kql
traces
| where customDimensions.ErrorCategory != ""
| extend errorCategory = tostring(customDimensions.ErrorCategory)
| extend protocol = tostring(customDimensions.Protocol)
| summarize ErrorCount = count() by errorCategory, protocol, bin(timestamp, 1h)
| render columnchart
```

Error categories include:
- `AuthenticationFailure`
- `ConnectionTimeout`
- `ProtocolError`
- `FileNotFound`
- `PermissionDenied`

## Dashboard Layout

### Overview Panel
- **Total Active Configurations**: Gauge
- **Success Rate (24h)**: Large number with trend
- **Files Discovered Today**: Count with sparkline
- **Current Failures**: Alert indicator

### Execution Health
- **Success Rate by Hour**: Line chart
- **Execution Duration (P50/P95/P99)**: Multi-line chart
- **Failures by Protocol**: Bar chart
- **Error Categories**: Pie chart

### Per-Client View
- **Active Configurations**: Table with client breakdown
- **Execution Success Rate**: Client comparison
- **Files Discovered**: Client ranking

### Protocol Performance
- **Duration by Protocol**: Box plot
- **Success Rate by Protocol**: Bar chart
- **Connection Errors**: Time series by protocol

## Alerts

### Critical Alerts

1. **Service Unavailability**
   - Condition: No successful executions in last 15 minutes
   - Severity: Critical
   - Action: Page on-call engineer

2. **High Failure Rate**
   - Condition: Success rate < 50% over 1 hour
   - Severity: Critical
   - Action: Investigate immediately

3. **Performance Degradation**
   - Condition: P95 duration > 60 seconds for 30 minutes
   - Severity: High
   - Action: Check infrastructure capacity

### Warning Alerts

4. **Moderate Failure Rate**
   - Condition: Success rate < 90% over 1 hour
   - Severity: Warning
   - Action: Review logs for patterns

5. **Slow Protocol**
   - Condition: Protocol-specific P95 > 45 seconds
   - Severity: Warning
   - Action: Check specific protocol adapter

6. **Authentication Failures**
   - Condition: > 5 auth failures in 1 hour
   - Severity: Warning
   - Action: Verify credentials in Key Vault

## Using the API for Monitoring

### Get Execution History
```http
GET /api/configuration/{configurationId}/executionhistory?pageSize=50&status=Failed&startDate=2025-01-20T00:00:00Z
Authorization: Bearer {token}
```

### Get Execution Metrics
```http
GET /api/configuration/{configurationId}/executionhistory/metrics?startDate=2025-01-01T00:00:00Z&endDate=2025-01-31T23:59:59Z
Authorization: Bearer {token}
```

Response includes:
- Total executions
- Success/failure counts
- Success rate
- Average duration
- Files discovered per day

## Troubleshooting Guide

### No Files Being Discovered
1. Check execution history for failures: `GET /api/configuration/{id}/executionhistory?status=Failed`
2. Review error category in failed executions
3. Verify configuration is active: `isActive = true`
4. Confirm schedule is evaluating correctly
5. Test protocol connectivity manually

### High Execution Duration
1. Check protocol-specific timeouts (default: 30-120s)
2. Review network latency to target systems
3. Verify file pattern complexity isn't excessive
4. Check concurrent execution count (max 100)

### Authentication Failures
1. Verify Key Vault secrets are current
2. Check service principal/managed identity permissions
3. Confirm credentials haven't expired
4. Review protocol-specific auth settings

### Configuration Not Executing
1. Verify schedule cron expression is valid
2. Check `NextScheduledRun` is in future
3. Confirm configuration is active
4. Review scheduler logs for skipped executions
5. Check concurrency limits (SC-004: max 100 concurrent)

## Performance Baselines

### Expected Performance (SC-002, SC-003, SC-004)
- **Scheduled execution accuracy**: Within 1 minute of scheduled time (99% of executions)
- **File discovery latency**: < 5 seconds from discovery to event publish
- **Concurrent capacity**: 100 concurrent file checks without degradation
- **Date token accuracy**: 100% (no failures)

### Typical Durations by Protocol
- **FTP**: 2-15 seconds (network dependent)
- **HTTPS**: 1-10 seconds (server response dependent)
- **Azure Blob**: 0.5-5 seconds (fastest, internal Azure network)

## Runbook References

For operational procedures, see:
- **Configuration Troubleshooting**: `/docs/runbook.md#configuration-not-executing`
- **Protocol Connectivity Issues**: `/docs/runbook.md#connection-failures`
- **Performance Degradation**: `/docs/runbook.md#slow-executions`

## Dashboard Export

This dashboard configuration can be imported into Azure Portal:
1. Navigate to Azure Portal → Dashboards
2. Click "Upload" → Select `dashboards/file-retrieval-monitoring.json`
3. Customize resource IDs for your Application Insights instance

---

**Last Updated**: 2025-01-24  
**Owner**: Platform Engineering Team  
**Related**: [deployment.md](./deployment.md), [runbook.md](./runbook.md)
