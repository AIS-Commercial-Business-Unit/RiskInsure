# Local Smoke Test Prompt

**Purpose**: Execute quick verification of locally running Docker services  
**Target**: All RiskInsure microservices (APIs and NServiceBus endpoints)  
**Execution Time**: 10-15 seconds  
**Non-Destructive**: Read-only checks, no data modifications

---

## Instructions for AI Agent

You are performing a smoke test of the RiskInsure local development environment. Follow these steps in order and report findings clearly.

### Step 1: Verify Docker Environment

**Check Docker daemon:**
```powershell
wsl docker version
```
- **Expected**: Client and Server versions displayed
- **If fails**: Prompt user to start Rancher Desktop

### Step 2: Check Container Status

**List all RiskInsure containers:**
```powershell
wsl docker ps --filter "name=riskinsure" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

**Expected services:**
1. `riskinsure-billing-api-1` → Port 7071
2. `riskinsure-billing-endpoint-1`
3. `riskinsure-customer-api-1` → Port 7073
4. `riskinsure-customer-endpoint-1`
5. `riskinsure-fundstransfermgt-api-1` → Port 7075
6. `riskinsure-fundstransfermgt-endpoint-1`
7. `riskinsure-policy-api-1` → Port 7077
8. `riskinsure-policy-endpoint-1`
9. `riskinsure-ratingandunderwriting-api-1` → Port 7079
10. `riskinsure-ratingandunderwriting-endpoint-1`

**For any exited containers**, retrieve logs:
```powershell
wsl docker logs <container-name> --tail 30
```

### Step 3: Test API Endpoints

**Test each API for basic connectivity:**

```powershell
# Billing API
try {
    $response = Invoke-WebRequest -Uri "http://localhost:7071" -Method GET -TimeoutSec 5 -UseBasicParsing
    Write-Host "✅ Billing API: $($response.StatusCode)" -ForegroundColor Green
} catch {
    Write-Host "❌ Billing API: $($_.Exception.Message)" -ForegroundColor Red
}
```

Repeat for:
- Customer: `http://localhost:7073`
- FundsTransferMgt: `http://localhost:7075`
- Policy: `http://localhost:7077`
- RatingAndUnderwriting: `http://localhost:7079`

**Acceptable responses:**
- `200 OK` - Service running with root endpoint
- `404 Not Found` - Service running, no root endpoint defined (still valid)
- `401 Unauthorized` - Service running, authentication required (still valid)
- **Connection refused/timeout** - Service NOT running (FAIL)

### Step 4: Validate Configuration

**Check .env file exists:**
```powershell
if (Test-Path .env) {
    Write-Host "✅ .env file exists" -ForegroundColor Green
} else {
    Write-Host "❌ .env file missing" -ForegroundColor Red
}
```

**Validate connection string format** (don't print values):
```powershell
$env = Get-Content .env | Out-String
if ($env -match "COSMOSDB_CONNECTION_STRING=AccountEndpoint=") {
    Write-Host "✅ Cosmos DB connection string present" -ForegroundColor Green
} else {
    Write-Host "❌ Invalid Cosmos DB connection string" -ForegroundColor Red
}

if ($env -match "SERVICEBUS_CONNECTION_STRING=Endpoint=sb://") {
    Write-Host "✅ Service Bus connection string valid format" -ForegroundColor Green
} else {
    Write-Host "❌ Invalid Service Bus connection string" -ForegroundColor Red
}
```

### Step 5: Check for Common Issues

**Check for crashed containers:**
```powershell
$crashed = wsl docker ps -a --filter "name=riskinsure" --filter "status=exited" --format "{{.Names}}: {{.Status}}"
if ($crashed) {
    Write-Host "⚠️ Crashed containers detected:" -ForegroundColor Yellow
    $crashed | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
} else {
    Write-Host "✅ No crashed containers" -ForegroundColor Green
}
```

**Check for restart loops:**
```powershell
$restarting = wsl docker ps --filter "name=riskinsure" --format "{{.Names}}: {{.Status}}" | Select-String "Restarting"
if ($restarting) {
    Write-Host "❌ Containers in restart loop:" -ForegroundColor Red
    $restarting | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
}
```

### Step 6: Generate Summary Report

**Create structured summary:**

```
========================================
 RiskInsure Local Smoke Test Results
========================================
Timestamp: <current-timestamp>

[DOCKER STATUS]
<status-of-docker-daemon>

[CONTAINER STATUS] (X/10 running)
<list-each-container-with-status>

[API CONNECTIVITY]
<test-results-for-each-api>

[CONFIGURATION]
<env-file-validation-results>

[ISSUES DETECTED]
<list-any-failures-or-warnings>

[OVERALL RESULT]
✅ PASS - All services operational
⚠️ PARTIAL PASS - X/10 services running
❌ FAIL - Critical services down

[NEXT STEPS]
<specific-actionable-recommendations>

[EXECUTION TIME]
<duration-in-seconds>
```

## Decision Tree

### If ALL services running and APIs responding:
✅ **PASS** - Report success, no action needed

### If 1-2 services down but not critical:
⚠️ **PARTIAL PASS**
1. Show which services are down
2. Retrieve their last 30 log lines
3. Suggest specific fixes based on error patterns
4. Recommend: `wsl docker-compose restart <service-name>`

### If 3+ services down or all APIs unreachable:
❌ **FAIL**
1. Check if Docker is actually running
2. Verify .env file configuration
3. Suggest: `.\scripts\docker-stop.ps1` then `.\scripts\docker-start.ps1`
4. Check WSL DNS configuration

### Common Error Patterns

**"Invalid URI: The hostname could not be parsed"**
→ Malformed Service Bus connection string in .env
→ Fix: Check for duplicate "Endpoint=" prefix

**"Exited (139)" (Segmentation fault)**
→ .NET runtime crash, usually DI configuration issue
→ Check Program.cs for missing registrations

**"Connection refused" on API port**
→ Container exited or not exposing port
→ Check `wsl docker logs <container-name>`

**DNS resolution failures**
→ WSL networking issue
→ Fix: `wsl sudo sh -c "echo 'nameserver 8.8.8.8' > /etc/resolv.conf"`

## Output Guidelines

- Use ✅ for passing checks (Green)
- Use ⚠️ for warnings (Yellow)
- Use ❌ for failures (Red)
- Use `[SECTION]` headers for organization
- Show container names as links when possible: `[riskinsure-billing-api-1](vscode://file/...)`
- Provide copy-paste commands for fixes
- Keep total output under 100 lines unless debugging

## Success Metrics

**Must have for PASS:**
- Docker daemon running
- At least 9/10 containers operational
- At least 4/5 API endpoints responding
- Valid .env configuration
- No containers in restart loops

**Nice to have:**
- All 10 containers running
- All APIs responding < 1 second
- No warnings in recent logs
- Clean startup logs (no retries)

## Related Documentation

- [Local Smoke Test Agent](../agents/local-smoke-test-agent.md) - Full agent specification
- [Docker Development](../../docs/docker-development.md) - Docker setup guide
- [Getting Started](../../docs/getting-started.md) - Initial setup instructions

## Usage

**Invoke this prompt with:**
```
@workspace run local smoke test
```

Or:
```
@workspace verify all local services are running
```

## Notes

- **Fast feedback**: Results in 10-15 seconds
- **Safe operation**: No writes, no deployments, no data changes
- **Clear output**: Easy to scan for issues
- **Actionable**: Suggests specific fixes for common problems
- **Idempotent**: Safe to run multiple times
