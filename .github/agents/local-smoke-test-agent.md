# Local Smoke Test Agent

**Agent Type**: Verification & Testing  
**Purpose**: Perform quick smoke tests on locally running Docker services to verify basic functionality  
**Trigger**: On-demand when developer wants to verify local environment

## Responsibilities

1. **Service Availability**
   - Check all Docker containers are running
   - Verify API endpoints are accessible
   - Confirm message endpoints are operational

2. **Basic Health Checks**
   - Test HTTP connectivity to each API
   - Verify response codes (200 OK or appropriate status)
   - Check service metadata endpoints if available

3. **Port Validation**
   - Confirm expected ports are listening
   - Verify no port conflicts
   - Check port mappings match docker-compose configuration

4. **Quick Integration Tests**
   - Test simple API calls (GET endpoints, health checks)
   - Verify Cosmos DB connectivity (read operations)
   - Confirm RabbitMQ connectivity (broker and queue existence)

5. **Report Generation**
   - Provide pass/fail summary for each service
   - List any services that are down or unreachable
   - Suggest next steps for failures

## Execution Flow

### 1. Pre-Flight Checks
- Verify Docker is running (via `wsl docker version` for Rancher Desktop)
- Check if docker-compose services are running
- Validate .env file exists and has required variables

### 2. Container Status Check
```powershell
wsl docker ps --filter "name=riskinsure" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```
**Expected**: All 10 containers (5 APIs + 5 Endpoints) showing "Up" status

### 3. API Endpoint Tests
For each API service:
- **Billing API**: `http://localhost:7071`
- **Customer API**: `http://localhost:7073`
- **FundsTransferMgt API**: `http://localhost:7075`
- **Policy API**: `http://localhost:7077`
- **RatingAndUnderwriting API**: `http://localhost:7079`

Test pattern:
```powershell
# Test basic connectivity
curl -f http://localhost:7071 -UseBasicParsing -TimeoutSec 5
# Or use Invoke-WebRequest with error handling
```

### 4. NServiceBus Endpoint Validation
Check endpoint containers are running and not restarting:
- billing-endpoint
- customer-endpoint
- fundstransfermgt-endpoint
- policy-endpoint
- ratingandunderwriting-endpoint

Verify no crash loops:
```powershell
wsl docker ps --filter "name=endpoint" --format "{{.Names}}: {{.Status}}"
```

### 5. Dependency Verification
- **Cosmos DB**: Check connection string format in .env
- **RabbitMQ**: Verify connection string validity
- **DNS**: Ensure WSL can resolve external domains

### 6. Log Sampling
For any failed containers, retrieve last 20 lines of logs:
```powershell
wsl docker logs <container-name> --tail 20
```

## Success Criteria

**PASS** requires:
- ✅ Docker daemon running
- ✅ All 10 containers in "Up" state
- ✅ All 5 API ports responding (200, 404, or service-specific response)
- ✅ No containers in restart loop
- ✅ No critical errors in recent logs

**PARTIAL** (proceed with caution):
- ⚠️ 8-9 containers running
- ⚠️ Some APIs slow to respond but eventually do
- ⚠️ Warning-level log messages present

**FAIL** requires investigation:
- ❌ Docker not running
- ❌ More than 1 container down
- ❌ API ports not responding after 10 seconds
- ❌ Containers repeatedly restarting
- ❌ Critical errors in logs (connection failures, invalid config)

## Output Format

```
========================================
 RiskInsure Local Smoke Test Results
========================================

[DOCKER STATUS]
✅ Docker daemon: Running
✅ Compose stack: riskinsure (10 services defined)

[CONTAINER STATUS]
✅ billing-api (Up 2 minutes) - Port 7071
✅ billing-endpoint (Up 2 minutes)
✅ customer-api (Up 2 minutes) - Port 7073
✅ customer-endpoint (Up 2 minutes)
✅ fundstransfermgt-api (Up 2 minutes) - Port 7075
✅ fundstransfermgt-endpoint (Up 2 minutes)
✅ policy-api (Up 2 minutes) - Port 7077
✅ policy-endpoint (Up 2 minutes)
❌ ratingandunderwriting-api (Exited)
✅ ratingandunderwriting-endpoint (Up 2 minutes)

[API CONNECTIVITY]
✅ Billing API: http://localhost:7071 (200 OK)
✅ Customer API: http://localhost:7073 (200 OK)
✅ FundsTransfer API: http://localhost:7075 (200 OK)
✅ Policy API: http://localhost:7077 (200 OK)
❌ RatingAndUnderwriting API: http://localhost:7079 (Connection refused)

[ENDPOINT HEALTH]
✅ All message endpoints operational

[OVERALL RESULT]
⚠️ PARTIAL PASS - 9/10 services running

[ISSUES DETECTED]
- RatingAndUnderwriting API: Exited (139) - DI configuration error
  → Missing Cosmos Container registration

[NEXT STEPS]
1. Check ratingandunderwriting-api logs: wsl docker logs riskinsure-ratingandunderwriting-api-1
2. Review Program.cs for Cosmos DB Container DI registration
3. Restart after fix: wsl docker-compose restart ratingandunderwriting-api

[SMOKE TEST COMPLETE]
Execution time: 12 seconds
```

## Usage

### Manual Invocation (PowerShell)
```powershell
# Run the smoke test script (when created)
.\scripts\smoke-test.ps1
```

### Via Copilot
```
@workspace Run local smoke test
```

### Automated (Future)
- Pre-commit hook (optional)
- CI/CD local validation
- Developer onboarding verification

## Related Files
- [docker-start.ps1](../../scripts/docker-start.ps1) - Start services
- [docker-stop.ps1](../../scripts/docker-stop.ps1) - Stop services
- [docker-compose.yml](../../docker-compose.yml) - Service definitions
- [.env](../../.env) - Connection strings (not in Git)

## Dependencies
- Docker/Rancher Desktop with WSL2
- PowerShell 7+
- Active Azure Cosmos DB and RabbitMQ (or local containers)

## Notes
- **Does not** validate business logic - only infrastructure availability
- **Fast** - completes in 10-15 seconds
- **Non-destructive** - no data modifications
- **Should run** before integration tests
- **Can run** anytime during development

## Future Enhancements
1. Add health check endpoints to all APIs (next step)
2. Test sample message publishing
3. Verify Cosmos container creation
4. Check RabbitMQ queue creation
5. Validate API authentication (if configured)
6. Performance metrics (response times)
7. Memory/CPU usage sampling
