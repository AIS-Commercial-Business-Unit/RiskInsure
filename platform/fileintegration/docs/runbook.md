# Operational Runbook: File Retrieval Configuration

**Purpose**: Troubleshooting guide for common operational scenarios in the File Retrieval system  
**Audience**: Operations staff, SREs, on-call engineers  
**Last Updated**: 2025-01-24

---

## Table of Contents

1. [Configuration Not Executing](#1-configuration-not-executing)
2. [Connection Failures](#2-connection-failures)
3. [File Not Found](#3-file-not-found)
4. [Authentication Errors](#4-authentication-errors)
5. [Performance Issues](#5-performance-issues)
6. [Duplicate Workflow Triggers](#6-duplicate-workflow-triggers)
7. [Schedule Drift](#7-schedule-drift)
8. [Health Check Failures](#8-health-check-failures)

---

## 1. Configuration Not Executing

### Symptom
A FileRetrievalConfiguration is marked as `IsActive = true` but no scheduled executions are occurring.

### Diagnostic Steps

1. **Check configuration status**:
   ```bash
   curl -H "Authorization: Bearer $TOKEN" \
     https://api.riskinsure.com/api/v1/configuration/{configurationId}
   ```
   Verify: `isActive = true`, `schedule.cronExpression` is valid

2. **Check last execution time**:
   ```bash
   curl -H "Authorization: Bearer $TOKEN" \
     https://api.riskinsure.com/api/v1/configuration/{configurationId}/executionhistory?pageSize=5
   ```
   Look for: `lastExecutedAt` timestamp, `nextScheduledRun` timestamp

3. **Check Worker logs** (Azure Container Apps):
   ```bash
   az containerapp logs show \
     --name file-retrieval-worker \
     --resource-group riskinsure-prod \
     --follow
   ```
   Look for: `SchedulerHostedService` log entries, "Evaluating schedules" messages

4. **Check NServiceBus message queue**:
   - Open Azure Service Bus Explorer
   - Check `FileRetrieval.Worker` queue for pending `ExecuteFileCheck` messages
   - Look for dead-letter messages in `FileRetrieval.Worker/$DeadLetterQueue`

### Common Causes

| Cause | Resolution |
|-------|------------|
| **Worker service not running** | Check Container App status, restart if needed: `az containerapp restart --name file-retrieval-worker --resource-group riskinsure-prod` |
| **Invalid cron expression** | Update configuration with valid cron expression (use https://crontab.guru for validation) |
| **Timezone misconfiguration** | Verify `schedule.timezone` is valid IANA timezone (e.g., "America/New_York", not "EST") |
| **Schedule in the past** | Check `nextScheduledRun` - if in past, may indicate clock drift or service downtime |
| **Configuration disabled** | Set `isActive = true` via API: `PUT /api/v1/configuration/{id}` |
| **NServiceBus connection issue** | Check Service Bus connection string in app settings, verify namespace exists |

### Resolution

1. If Worker not running:
   ```bash
   az containerapp restart --name file-retrieval-worker --resource-group riskinsure-prod
   ```

2. If invalid schedule:
   ```bash
   curl -X PUT -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -H "If-Match: $ETAG" \
     -d '{"schedule": {"cronExpression": "0 8 * * *", "timezone": "America/New_York"}}' \
     https://api.riskinsure.com/api/v1/configuration/{configurationId}
   ```

3. Monitor next execution:
   ```bash
   # Watch Worker logs for next scheduled execution
   az containerapp logs show --name file-retrieval-worker --resource-group riskinsure-prod --follow
   ```

---

## 2. Connection Failures

### Symptom
FileRetrievalExecution records show `Status = Failed` with `ErrorCategory = "ConnectionFailure"` or `"ConnectionTimeout"`.

### Diagnostic Steps

1. **Check execution error details**:
   ```bash
   curl -H "Authorization: Bearer $TOKEN" \
     https://api.riskinsure.com/api/v1/configuration/{configurationId}/executionhistory/{executionId}
   ```
   Look for: `errorMessage`, `errorCategory`, `retryCount`

2. **Check Application Insights** for detailed error traces:
   ```kusto
   traces
   | where customDimensions.ConfigurationId == "{configurationId}"
   | where timestamp > ago(1h)
   | where severityLevel >= 3  // Warning or Error
   | project timestamp, message, customDimensions.Protocol, customDimensions.ErrorCategory
   | order by timestamp desc
   ```

3. **Test connection manually**:
   - **FTP**: Use FTP client (FileZilla, WinSCP) with same credentials
   - **HTTPS**: Use `curl` or browser to test URL
   - **Azure Blob**: Use Azure Storage Explorer with same connection string

### Common Causes

| Protocol | Cause | Resolution |
|----------|-------|------------|
| **FTP** | Firewall blocking connection | Verify FTP server IP/port is accessible from Azure Container Apps, check NSG rules |
| **FTP** | Passive mode issues | Try active mode: set `usePassiveMode = false` in configuration |
| **FTP** | Connection timeout too short | Increase `connectionTimeout` in ProtocolSettings (default 30s) |
| **HTTPS** | TLS/SSL certificate error | Verify HTTPS endpoint has valid certificate, check certificate chain |
| **HTTPS** | Redirect loop | Check `maxRedirects` setting, verify endpoint doesn't redirect infinitely |
| **Azure Blob** | Managed Identity not assigned | Assign managed identity to Container App, grant "Storage Blob Data Reader" role |
| **Azure Blob** | SAS token expired | Update SAS token in Key Vault with new expiry date |
| **All** | Network connectivity | Check Azure Container Apps VNet configuration, verify outbound connectivity |

### Resolution

1. **Increase timeout** (if intermittent):
   ```json
   {
     "protocolSettings": {
       "connectionTimeout": "00:01:00"  // Increase to 60 seconds
     }
   }
   ```

2. **Update credentials** (if authentication error):
   ```bash
   # Update Key Vault secret
   az keyvault secret set \
     --vault-name riskinsure-secrets \
     --name acme-ftp-password \
     --value "newpassword"
   ```

3. **Test from Container App**:
   ```bash
   # Exec into Container App for network diagnostics
   az containerapp exec \
     --name file-retrieval-worker \
     --resource-group riskinsure-prod \
     --command "/bin/bash"
   
   # Test connectivity
   curl -v https://client-server.com/files/
   nc -zv ftp.client.com 21
   ```

---

## 3. File Not Found

### Symptom
Executions complete successfully (`Status = Completed`) but `FilesFound = 0` when files are expected.

### Diagnostic Steps

1. **Check resolved patterns**:
   ```bash
   curl -H "Authorization: Bearer $TOKEN" \
     https://api.riskinsure.com/api/v1/configuration/{configurationId}/executionhistory/{executionId}
   ```
   Look at: `resolvedFilePathPattern`, `resolvedFilenamePattern`

2. **Verify tokens are replaced correctly**:
   - Example: If today is 2025-01-24, `{yyyy}/{mm}/{dd}` should resolve to `2025/01/24`
   - Check if file actually exists at resolved path: `/transactions/2025/01/24/trans_20250124.csv`

3. **Check actual file location** (manual verification):
   - Log into FTP server or browse HTTPS endpoint
   - Navigate to resolved path
   - Verify file exists with exact name (case-sensitive!)

### Common Causes

| Cause | Resolution |
|-------|------------|
| **Incorrect date tokens** | File may use different date format (e.g., `{yyyymmdd}` vs `{yyyy}{mm}{dd}`) |
| **Case sensitivity** | Filename on server is `Trans_20250124.csv` but pattern expects `trans_20250124.csv` (Unix/Linux are case-sensitive) |
| **File uploaded late** | File may not be available at scheduled check time, schedule earlier or add delay |
| **Wrong timezone** | Check may run at 8 AM UTC instead of 8 AM client timezone - verify `schedule.timezone` |
| **Pattern too specific** | Pattern matches `trans_20250124.csv` but actual file is `trans_20250124_v2.csv` - use wildcards if needed |
| **Incorrect base path** | FTP path starts at `/home/user/` but pattern assumes `/` - adjust `filePathPattern` |

### Resolution

1. **Update pattern to match actual file naming**:
   ```json
   {
     "filenamePattern": "trans_{yyyy}{mm}{dd}*.csv",  // Add wildcard for versions
     "filePathPattern": "/home/user/transactions/{yyyy}/{mm}"  // Include base path
   }
   ```

2. **Test token replacement manually**:
   - Use test endpoint (if available) to validate token replacement logic
   - Check logs for "Token replacement result" entries

3. **Adjust schedule to account for file upload timing**:
   ```json
   {
     "schedule": {
       "cronExpression": "30 8 * * *",  // 8:30 AM instead of 8:00 AM
       "timezone": "America/New_York"
     }
   }
   ```

---

## 4. Authentication Errors

### Symptom
Executions fail with `ErrorCategory = "AuthenticationFailure"`.

### Diagnostic Steps

1. **Check error message**:
   ```bash
   curl -H "Authorization: Bearer $TOKEN" \
     https://api.riskinsure.com/api/v1/configuration/{configurationId}/executionhistory/{executionId}
   ```
   Look for: "Invalid credentials", "401 Unauthorized", "403 Forbidden"

2. **Verify Key Vault secrets exist**:
   ```bash
   az keyvault secret show \
     --vault-name riskinsure-secrets \
     --name acme-ftp-password
   ```

3. **Check managed identity assignment** (for Azure Blob with ManagedIdentity):
   ```bash
   az containerapp identity show \
     --name file-retrieval-worker \
     --resource-group riskinsure-prod
   ```

### Common Causes

| Protocol | Cause | Resolution |
|----------|-------|------------|
| **FTP** | Password expired or changed | Update Key Vault secret with new password |
| **FTP** | Username incorrect | Update configuration with correct username |
| **HTTPS** | Bearer token expired | Refresh token in Key Vault |
| **HTTPS** | API key revoked | Update API key in Key Vault |
| **Azure Blob** | Managed identity not assigned | Assign identity: `az containerapp identity assign --name file-retrieval-worker --resource-group riskinsure-prod` |
| **Azure Blob** | Missing RBAC role | Grant "Storage Blob Data Reader" role on storage account |
| **Azure Blob** | Connection string invalid | Update connection string in Key Vault |
| **All** | Key Vault secret not found | Create secret: `az keyvault secret set --vault-name riskinsure-secrets --name {secretName} --value {password}` |

### Resolution

1. **Update credentials in Key Vault**:
   ```bash
   az keyvault secret set \
     --vault-name riskinsure-secrets \
     --name acme-ftp-password \
     --value "newpassword123"
   ```

2. **Grant RBAC role** (Azure Blob with Managed Identity):
   ```bash
   # Get managed identity principal ID
   PRINCIPAL_ID=$(az containerapp identity show \
     --name file-retrieval-worker \
     --resource-group riskinsure-prod \
     --query principalId -o tsv)
   
   # Grant Storage Blob Data Reader role
   az role assignment create \
     --assignee $PRINCIPAL_ID \
     --role "Storage Blob Data Reader" \
     --scope "/subscriptions/{subscriptionId}/resourceGroups/{rg}/providers/Microsoft.Storage/storageAccounts/{accountName}"
   ```

3. **Test authentication manually**:
   ```bash
   # FTP
   ftp ftp.client.com
   # Enter username and password
   
   # HTTPS with Basic Auth
   curl -u username:password https://client-server.com/files/
   
   # Azure Blob with connection string
   az storage blob list \
     --container-name transactions \
     --connection-string "DefaultEndpointsProtocol=https;..."
   ```

---

## 5. Performance Issues

### Symptom
- Scheduled checks taking longer than expected (DurationMs > 10 seconds)
- API responses slow (> 2 seconds)
- High CPU/memory usage on Worker

### Diagnostic Steps

1. **Check execution duration metrics**:
   ```kusto
   customMetrics
   | where name == "FileCheckDuration"
   | where timestamp > ago(24h)
   | summarize avg(value), max(value), percentile(value, 95) by bin(timestamp, 1h), tostring(customDimensions.Protocol)
   ```

2. **Check concurrent execution count**:
   ```kusto
   traces
   | where message contains "Starting file check"
   | where timestamp > ago(1h)
   | summarize count() by bin(timestamp, 1m)
   ```

3. **Check Cosmos DB RU consumption**:
   ```bash
   az cosmosdb show \
     --name riskinsure-cosmos \
     --resource-group riskinsure-prod \
     --query "documentEndpoint"
   ```
   Open Azure Portal → Cosmos DB → Metrics → Request Units

### Common Causes

| Cause | Resolution |
|-------|------------|
| **Too many concurrent checks** | Default limit is 100 concurrent checks (SC-004), reduce active configurations or increase worker instances |
| **Slow protocol connection** | Increase connection timeout, check network latency to remote servers |
| **Large file listings** | Azure Blob with 10,000+ files in container - add `BlobPrefix` filter to reduce listing scope |
| **Cosmos DB throttling** | Increase RU/s allocation or use autoscale |
| **High retry rate** | Many failed checks causing retries - fix underlying connection/auth issues first |
| **Too many configurations** | 1000+ configurations may overwhelm single worker - scale horizontally |

### Resolution

1. **Scale Worker horizontally**:
   ```bash
   az containerapp update \
     --name file-retrieval-worker \
     --resource-group riskinsure-prod \
     --min-replicas 2 \
     --max-replicas 5
   ```

2. **Increase Cosmos DB throughput** (if throttling):
   ```bash
   az cosmosdb sql database throughput update \
     --account-name riskinsure-cosmos \
     --resource-group riskinsure-prod \
     --name FileRetrieval \
     --throughput 1000  # Increase from 400 to 1000 RU/s
   ```

3. **Add blob prefix filter** (Azure Blob only):
   ```json
   {
     "protocolSettings": {
       "blobPrefix": "transactions/2025/"  // Narrow down listing scope
     }
   }
   ```

4. **Optimize schedule density**:
   - Stagger schedules to avoid all checks at same time
   - Instead of 100 configs at "0 8 * * *", spread across "0-59 8 * * *"

---

## 6. Duplicate Workflow Triggers

### Symptom
Multiple workflow instances started for the same file (violation of SC-007: zero duplicate triggers).

### Diagnostic Steps

1. **Check DiscoveredFile records**:
   ```bash
   curl -H "Authorization: Bearer $TOKEN" \
     https://api.riskinsure.com/api/v1/configuration/{configurationId}/discoveredfiles?date=2025-01-24
   ```
   Look for: Multiple records with same `fileUrl` and `discoveryDate`

2. **Check Service Bus messages**:
   - Open Service Bus Explorer
   - Check `FileDiscovered` topic subscriptions
   - Look for duplicate messages with same `IdempotencyKey`

3. **Query Application Insights**:
   ```kusto
   traces
   | where customDimensions.EventType == "FileDiscovered"
   | where customDimensions.FileUrl == "{fileUrl}"
   | where timestamp > ago(24h)
   | summarize count() by tostring(customDimensions.IdempotencyKey)
   | where count_ > 1  // Duplicate events
   ```

### Root Cause Analysis

The system enforces idempotency at multiple levels:
1. **DiscoveredFile unique constraint**: `(clientId, configurationId, fileUrl, discoveryDate)`
2. **Cosmos DB ETag checks**: Prevent duplicate inserts
3. **IdempotencyKey on messages**: Same file on same day = same idempotency key

If duplicates occur, likely causes:

| Cause | Resolution |
|-------|------------|
| **Unique constraint not enforced** | Verify Cosmos DB unique key policy on `discovered-files` container, recreate container if needed |
| **Multiple Worker instances without coordination** | Implement distributed locking in SchedulerHostedService (Cosmos DB lease) |
| **Clock skew causing date boundary issues** | File discovered at 23:59:59 and 00:00:01 = different `discoveryDate` = different record (expected) |
| **IdempotencyKey collision** | Very rare - review idempotency key generation logic |
| **Message handler not idempotent** | Review `ExecuteFileCheckHandler` - should check for existing DiscoveredFile before publishing |

### Resolution

1. **Verify unique constraint**:
   ```bash
   # Check container unique key policy
   az cosmosdb sql container show \
     --account-name riskinsure-cosmos \
     --resource-group riskinsure-prod \
     --database-name FileRetrieval \
     --name discovered-files \
     --query "resource.uniqueKeyPolicy"
   ```

2. **Enable distributed locking** (if running multiple workers):
   - Check configuration: `Scheduling:UseLease = true` in appsettings.json
   - Verify lease container exists in Cosmos DB

3. **Investigate date boundary case**:
   - If duplicates occur around midnight, this may be expected behavior (same file, different day)
   - Review business requirement: Should system trigger workflow once per file per day?

---

## 7. Schedule Drift

### Symptom
Scheduled checks executing later than expected (violates SC-002: 99% within 1 minute of scheduled time).

### Diagnostic Steps

1. **Calculate execution lag**:
   ```kusto
   customEvents
   | where name == "ScheduledCheckExecuted"
   | extend scheduledTime = todatetime(customDimensions.ScheduledTime)
   | extend actualTime = todatetime(customDimensions.ActualExecutionTime)
   | extend lagSeconds = datetime_diff('second', actualTime, scheduledTime)
   | where lagSeconds > 60  // More than 1 minute late
   | summarize count(), avg(lagSeconds), max(lagSeconds) by bin(timestamp, 1h)
   ```

2. **Check Worker resource utilization**:
   ```bash
   az monitor metrics list \
     --resource "/subscriptions/{sub}/resourceGroups/riskinsure-prod/providers/Microsoft.App/containerApps/file-retrieval-worker" \
     --metric "CpuPercentage" "MemoryPercentage" \
     --start-time "2025-01-24T00:00:00Z" \
     --end-time "2025-01-24T23:59:59Z"
   ```

### Common Causes

| Cause | Resolution |
|-------|------------|
| **Worker overloaded** | Too many concurrent checks, scale horizontally |
| **Scheduler polling interval too long** | Default is 1 minute - reduce if needed (caution: more Cosmos DB queries) |
| **NServiceBus message queue backlog** | Messages piling up faster than handlers can process - scale workers |
| **Long-running checks blocking scheduler** | Slow protocol connections delaying next iteration - implement async execution |
| **Container App cold start** | Worker restarting frequently - increase min replicas to keep warm instances |

### Resolution

1. **Increase worker instances**:
   ```bash
   az containerapp update \
     --name file-retrieval-worker \
     --resource-group riskinsure-prod \
     --min-replicas 3
   ```

2. **Reduce polling interval** (if needed):
   ```json
   {
     "Scheduling": {
       "PollingIntervalSeconds": 30  // Check every 30 seconds instead of 60
     }
   }
   ```

3. **Monitor queue depth**:
   ```bash
   az servicebus queue show \
     --namespace-name riskinsure-servicebus \
     --resource-group riskinsure-prod \
     --name FileRetrieval.Worker \
     --query "messageCount"
   ```

---

## 8. Health Check Failures

### Symptom
Health check endpoints returning unhealthy status, Container App showing as not ready.

### Diagnostic Steps

1. **Check health endpoint directly**:
   ```bash
   # Liveness probe (should always be healthy if app is running)
   curl https://api.riskinsure.com/health/live
   
   # Readiness probe (checks Cosmos DB connectivity)
   curl https://api.riskinsure.com/health/ready
   
   # Full health check
   curl https://api.riskinsure.com/health
   ```

2. **Check Container App logs**:
   ```bash
   az containerapp logs show \
     --name file-retrieval-api \
     --resource-group riskinsure-prod \
     --follow
   ```
   Look for: Health check execution logs, Cosmos DB connection errors

3. **Check Cosmos DB connectivity**:
   ```bash
   # Test from local machine
   az cosmosdb show --name riskinsure-cosmos --resource-group riskinsure-prod
   ```

### Common Causes

| Check | Cause | Resolution |
|-------|-------|------------|
| **Liveness** | App crashed or deadlocked | Restart Container App: `az containerapp restart --name file-retrieval-api --resource-group riskinsure-prod` |
| **Readiness** | Cosmos DB unreachable | Check Cosmos DB firewall rules, verify Container App has network access |
| **Readiness** | Cosmos DB throttling | Increase RU/s or reduce request rate |
| **Readiness** | Invalid connection string | Verify `CosmosDb:Endpoint` and `CosmosDb:Key` in app settings |

### Resolution

1. **Fix Cosmos DB connectivity**:
   ```bash
   # Check firewall rules
   az cosmosdb show \
     --name riskinsure-cosmos \
     --resource-group riskinsure-prod \
     --query "ipRules"
   
   # Add Container App subnet to allowed networks
   az cosmosdb update \
     --name riskinsure-cosmos \
     --resource-group riskinsure-prod \
     --enable-virtual-network true \
     --virtual-network-rules "/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/virtualNetworks/{vnet}/subnets/{subnet}"
   ```

2. **Temporarily disable health checks** (emergency only):
   ```bash
   az containerapp update \
     --name file-retrieval-api \
     --resource-group riskinsure-prod \
     --set properties.template.containers[0].probes=null
   ```

3. **Monitor health status**:
   ```bash
   watch -n 5 'curl -s https://api.riskinsure.com/health | jq ".status"'
   ```

---

## Common Log Queries

### Find all failures in last 24 hours
```kusto
traces
| where customDimensions.ExecutionStatus == "Failed"
| where timestamp > ago(24h)
| project timestamp, message, 
          tostring(customDimensions.ClientId),
          tostring(customDimensions.ConfigurationId),
          tostring(customDimensions.ErrorCategory),
          tostring(customDimensions.Protocol)
| order by timestamp desc
```

### Track configuration execution success rate
```kusto
customEvents
| where name == "FileCheckCompleted" or name == "FileCheckFailed"
| where timestamp > ago(7d)
| summarize 
    Total = count(),
    Succeeded = countif(name == "FileCheckCompleted"),
    Failed = countif(name == "FileCheckFailed")
    by tostring(customDimensions.ConfigurationId), bin(timestamp, 1d)
| extend SuccessRate = round(100.0 * Succeeded / Total, 2)
| order by timestamp desc
```

### Find slow executions (> 10 seconds)
```kusto
customMetrics
| where name == "FileCheckDuration"
| where value > 10000  // milliseconds
| where timestamp > ago(24h)
| project timestamp, value, 
          tostring(customDimensions.ConfigurationId),
          tostring(customDimensions.Protocol),
          tostring(customDimensions.ClientId)
| order by value desc
```

---

## Escalation Contacts

| Issue Type | Contact | Response Time |
|------------|---------|---------------|
| Configuration issues | Platform Team | 1 hour (business hours) |
| Connection failures | Infrastructure Team + Client IT | 2 hours |
| Duplicate triggers | Platform Team (critical) | 30 minutes |
| Performance degradation | Platform Team + Azure Support | 1 hour |
| Security/credential issues | Security Team + Client IT | 15 minutes (critical) |

---

## Additional Resources

- **Architecture**: [services/file-retrieval/docs/file-retrieval-standards.md](file-retrieval-standards.md)
- **Deployment Guide**: [services/file-retrieval/docs/deployment.md](deployment.md)
- **Monitoring Dashboard**: [services/file-retrieval/docs/monitoring.md](monitoring.md)
- **Quickstart Guide**: [specs/001-file-retrieval-config/quickstart.md](../../specs/001-file-retrieval-config/quickstart.md)
- **Data Model**: [specs/001-file-retrieval-config/data-model.md](../../specs/001-file-retrieval-config/data-model.md)
- **Contracts**: [specs/001-file-retrieval-config/contracts/](../../specs/001-file-retrieval-config/contracts/)

---

## Revision History

| Date | Author | Changes |
|------|--------|---------|
| 2025-01-24 | Platform Team | Initial version (T148) |
