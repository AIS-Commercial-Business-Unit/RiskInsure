# File Retrieval Scheduler Configuration

## Overview

The File Retrieval Worker includes a background scheduler that periodically checks for file retrieval configurations that are due to run based on their CRON schedules. When a configuration is due, the scheduler sends an `ExecuteFileCheck` command to trigger the file retrieval process.

## Configuration

The scheduler behavior is controlled via the `Scheduler` section in `appsettings.json`:

```json
{
  "Scheduler": {
    "PollingIntervalSeconds": 60,
    "ExecutionWindowMinutes": 2,
    "MaxConcurrentChecks": 100,
    "EnableDistributedLocking": true
  }
}
```

### Configuration Options

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `PollingIntervalSeconds` | int | 60 | How often the scheduler checks for due configurations (1-3600 seconds) |
| `ExecutionWindowMinutes` | int | 2 | Time window to consider a configuration "due" for execution (1-60 minutes) |
| `MaxConcurrentChecks` | int | 100 | Maximum number of concurrent file checks (1-1000) |
| `EnableDistributedLocking` | bool | true | Enable distributed locking to prevent duplicate processing across instances |

### Polling Interval

The `PollingIntervalSeconds` setting controls how frequently the scheduler queries Cosmos DB for configurations that need to run:

- **Default**: 60 seconds (1 minute)
- **Minimum**: 1 second
- **Maximum**: 3600 seconds (1 hour)
- **Recommended**: 60 seconds for most scenarios

**Example configurations:**

```json
// Check every 30 seconds (more responsive)
"PollingIntervalSeconds": 30

// Check every 5 minutes (less frequent, reduces DB queries)
"PollingIntervalSeconds": 300
```

### Execution Window

The `ExecutionWindowMinutes` defines the time window in which a configuration is considered "due":

- If a configuration's next scheduled time falls within this window from the current time, it will be triggered
- Helps handle clock skew and processing delays
- **Default**: 2 minutes
- **Range**: 1-60 minutes

### Concurrency Control

The `MaxConcurrentChecks` setting limits how many file checks can run simultaneously:

- Prevents overwhelming external systems (FTP servers, blob storage)
- When limit is reached, additional checks are deferred to the next polling cycle
- **Default**: 100
- **Range**: 1-1000

### Distributed Locking

The `EnableDistributedLocking` setting (future implementation) will prevent multiple worker instances from processing the same configuration:

- **true**: Use distributed locking (recommended for production with multiple instances)
- **false**: No locking (suitable for single-instance development)

## How It Works

1. **Initialization**: SchedulerHostedService starts 5 seconds after the worker starts
2. **Polling Loop**: Every `PollingIntervalSeconds`, the scheduler:
   - Queries Cosmos DB for all active file retrieval configurations
   - Evaluates each configuration's CRON schedule using the `ScheduleEvaluator`
   - Determines which configurations are due within the `ExecutionWindowMinutes` window
   - Sends `ExecuteFileCheck` commands for due configurations
3. **Concurrency**: Uses a semaphore to limit concurrent checks to `MaxConcurrentChecks`
4. **Idempotency**: Tracks in-progress checks to prevent duplicate processing within the same instance

## Scheduler Behavior

### CRON Schedule Evaluation

The scheduler uses **NCrontab** to parse and evaluate CRON expressions:

- **Format**: Standard 5-field CRON (minute, hour, day, month, day-of-week)
- **Timezone**: Each configuration has its own timezone setting
- **Calculation**: Next execution time is calculated in the configuration's timezone, then converted to UTC

### Example Schedules

| CRON Expression | Description |
|----------------|-------------|
| `0 9 * * *` | Daily at 9:00 AM |
| `0 */4 * * *` | Every 4 hours |
| `0 0 1 * *` | First day of each month at midnight |
| `0 9 * * 1-5` | Weekdays at 9:00 AM |

### Execution Window Logic

```
Current Time: 10:00:00 UTC
Execution Window: 2 minutes
Configuration Next Run: 10:01:30 UTC

Time Difference: 1.5 minutes < 2 minutes
Result: Configuration is DUE, ExecuteFileCheck command sent
```

### Missed Executions

If a configuration's scheduled time has passed (negative time difference):

- A warning is logged: "Configuration has overdue execution"
- The configuration is **not** triggered automatically
- The next valid schedule occurrence will be used on the next polling cycle

## Logging

The scheduler emits structured logs at various levels:

### Information (Normal Operation)

```
SchedulerHostedService starting - polling interval: 00:01:00, max concurrent: 100, execution window: 00:02:00
Scheduler check completed: 3 triggered, 0 skipped (in-progress), 0 deferred (concurrency limit), 25 active configurations
```

### Debug (Detailed Execution)

```
Checking for scheduled file retrieval configurations at 2026-02-23T17:45:00.000Z
Found 25 active configurations to check
Triggering file check for configuration abc123 (Client: client1, Name: 'Daily File Import', Protocol: FTP)
ExecuteFileCheck command sent for configuration abc123
```

### Warning (Issues)

```
Configuration abc123 has overdue execution (scheduled: 2026-02-23T17:30:00Z, current: 2026-02-23T17:45:00Z)
Concurrent execution limit reached (100). Configuration xyz789 will be deferred.
```

### Error (Failures)

```
Error checking scheduled configurations: Connection timeout
Error triggering file check for configuration abc123: NServiceBus send failed
```

## Performance Considerations

### Cosmos DB Query Patterns

The scheduler uses `GetAllActiveConfigurationsAsync()` which:

- Queries all active configurations across all clients
- **Current Implementation**: Full scan (may be slow with 1000+ configurations)
- **Recommended Enhancement**: Add secondary index on `NextScheduledRun` for efficient queries

### Polling Frequency Trade-offs

| Interval | Pros | Cons |
|----------|------|------|
| 10-30 seconds | More responsive, minimal delay | Higher DB query load |
| 60 seconds (default) | Balanced responsiveness and load | Up to 1 minute delay |
| 300+ seconds | Lower DB load | Longer delays, may miss tight windows |

### Scaling Recommendations

- **Single Instance**: Use default settings (60s polling, 100 concurrent)
- **Multiple Instances**: Enable `EnableDistributedLocking` when available
- **High Volume** (1000+ configs): Consider increasing `PollingIntervalSeconds` to 120-300 seconds
- **Real-time Requirements**: Decrease to 10-30 seconds, add Cosmos DB indexing

## Monitoring

### Key Metrics to Track

1. **Triggered Count**: Number of configurations triggered per polling cycle
2. **Skipped Count**: Configurations skipped due to in-progress checks
3. **Deferred Count**: Configurations deferred due to concurrency limit
4. **Active Count**: Total active configurations
5. **Processing Duration**: Time to complete each polling cycle

### Health Indicators

- ✅ **Healthy**: `Deferred Count = 0`, `Overdue Count = 0`
- ⚠️ **Warning**: `Deferred Count > 0` (increase `MaxConcurrentChecks` or add instances)
- ❌ **Critical**: `Overdue Count > 10` (scheduler falling behind, increase polling frequency)

## Development Examples

### Example 1: Fast Polling for Development

```json
{
  "Scheduler": {
    "PollingIntervalSeconds": 10,
    "ExecutionWindowMinutes": 1,
    "MaxConcurrentChecks": 10,
    "EnableDistributedLocking": false
  }
}
```

### Example 2: Production Multi-Instance

```json
{
  "Scheduler": {
    "PollingIntervalSeconds": 60,
    "ExecutionWindowMinutes": 2,
    "MaxConcurrentChecks": 100,
    "EnableDistributedLocking": true
  }
}
```

### Example 3: High-Volume Batch Processing

```json
{
  "Scheduler": {
    "PollingIntervalSeconds": 300,
    "ExecutionWindowMinutes": 5,
    "MaxConcurrentChecks": 200,
    "EnableDistributedLocking": true
  }
}
```

## Troubleshooting

### Issue: Configurations Not Triggering

**Possible Causes:**
1. `IsActive = false` on configuration
2. CRON expression doesn't match current time
3. Timezone mismatch
4. Scheduler service not running

**Resolution:**
- Check configuration status in Cosmos DB
- Validate CRON expression using online tools
- Verify timezone matches expected behavior
- Check worker logs for "SchedulerHostedService starting"

### Issue: High CPU/Memory Usage

**Possible Causes:**
1. `PollingIntervalSeconds` too low (< 10 seconds)
2. Too many active configurations (1000+)
3. `MaxConcurrentChecks` set too high

**Resolution:**
- Increase polling interval to 60+ seconds
- Add pagination to `GetAllActiveConfigurationsAsync`
- Reduce `MaxConcurrentChecks` to 50-100

### Issue: Configurations Running Late

**Possible Causes:**
1. `ExecutionWindowMinutes` too narrow
2. Concurrency limit reached (check logs for "deferred")
3. Scheduler falling behind

**Resolution:**
- Increase `ExecutionWindowMinutes` to 5+ minutes
- Increase `MaxConcurrentChecks`
- Add more worker instances
- Increase `PollingIntervalSeconds` to reduce query load

## Future Enhancements

1. **Distributed Locking**: Implement Redis-based locking for multi-instance coordination
2. **Cosmos DB Indexing**: Add secondary index on `NextScheduledRun` for efficient queries
3. **Metrics Endpoint**: Expose scheduler metrics via `/metrics` endpoint
4. **Dynamic Configuration**: Support changing polling interval without restart
5. **Smart Polling**: Adjust polling frequency based on configuration density
