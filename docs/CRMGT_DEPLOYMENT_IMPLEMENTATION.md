# CustomerRelationshipsMgt (crmgt-app) Deployment Implementation Summary

**Date**: March 9, 2026  
**Status**: ✅ All infrastructure configuration changes implemented  
**Next Steps**: Service code verification, local Docker deployment testing, and CI/CD validation

---

## 📋 Changes Completed

### 1. ✅ Terraform Infrastructure Configuration

#### `platform/infra/services/crmgt-app.tf` (NEW - 233 lines)
- **Created**: New Container Apps resource definitions for crmgt service
- **Resources**:
  - `azurerm_container_app.crmgt_api` - HTTP API endpoint on port 8080
  - `azurerm_container_app.crmgt_endpoint` - NServiceBus message handler
- **Configuration**:
  - Uses shared managed identity: `apps_shared_identity_id`
  - Pulls images from ACR: `{acr}/crmgt-api:${var.image_tag}` and `{acr}/crmgt-endpoint:${var.image_tag}`
  - Cosmos DB container: `customerrelationships`
  - Service Bus namespace configured via shared secrets
  - Application Insights integration enabled
  - Ingress on port 8080 (API only, endpoint has no external ingress)

#### `platform/infra/services/variables.tf` (UPDATED)
- **Added**: `"customerrelationshipsmgt"` entry to services map (lines ~280-295)
- **Configuration**:
  - API: cpu=0.25, memory=0.5Gi, min_replicas=1, max_replicas=10 (production-scaled)
  - Endpoint: cpu=0.25, memory=0.5Gi, min_replicas=1, max_replicas=5 (production-scaled)
  - Container name: `customerrelationshipsmgt`

#### `platform/infra/shared-services/cosmosdb.tf` (UPDATED)
- **Added**: `azurerm_cosmosdb_sql_container.customerrelationships` (lines ~158-171)
  - Container name: `customerrelationships`
  - Partition key: `/customerId`
  - Throughput: `var.cosmosdb_throughput` (inherits from variables)
  - Indexing: consistent across all paths
- **Added**: `azurerm_cosmosdb_sql_container.customerrelationshipsmgt_sagas` (lines ~287-302)
  - For NServiceBus saga orchestration
  - Partition key: `/customerId`
  - Same throughput configuration

#### `platform/infra/shared-services/servicebus.tf` (UPDATED)
- **Queue additions**:
  - `RiskInsure.CustomerRelationshipsMgt.Endpoint` (NServiceBus endpoint)
  - `riskinsure.customerrelationshipsmgt.api` (send-only API endpoint)
- **Topic additions** (4 domain events):
  - `RiskInsure.CustomerRelationshipsMgt.Domain.Contracts.Events.RelationshipCreated`
  - `RiskInsure.CustomerRelationshipsMgt.Domain.Contracts.Events.RelationshipInformationUpdated`
  - `RiskInsure.CustomerRelationshipsMgt.Domain.Contracts.Events.ContactInformationChanged`
  - `RiskInsure.CustomerRelationshipsMgt.Domain.Contracts.Events.RelationshipClosed`

#### Environment-Specific Variables (UPDATED)
- `platform/infra/services/dev.tfvars`: Updated comment to include customerrelationshipsmgt
- `platform/infra/services/prod.tfvars`: Updated comment to include customerrelationshipsmgt
- Both use defaults from variables.tf for service configuration

---

### 2. ✅ CI/CD Pipeline Configuration

#### `.github/workflows/ci-build-services.yml` (UPDATED)
- **ALL_SERVICES array** (line ~67): Added `"customerrelationshipsmgt"`
  - Now: `["billing","customer","customerrelationshipsmgt","fundstransfermgt","policy","policyequityandinvoicingmgt","ratingandunderwriting"]`
- **changed_services loop** (line ~138): Added customerrelationshipsmgt to change detection
  - Will automatically detect changes in `services/customerrelationshipsmgt/` and trigger builds
- **Impact**: CI now builds crmgt-api and crmgt-endpoint Docker images to ACR

#### `.github/workflows/cd-services-dev.yml` (UPDATED)
- **ALL_SERVICES array** (line ~64): Added `"customerrelationshipsmgt"`
- **changed_services loop** (line ~139): Added customerrelationshipsmgt to deployment detection
- **Impact**: Dev deployments will automatically include crmgt-app when triggered

---

### 3. ✅ Local Development Docker Configuration

#### `docker-compose.domain.yml` (UPDATED)
- **crmgt-api service** (NEW):
  - Port: `7083:8080` (avoiding conflicts with other services)
  - Build context: `services/customerrelationshipsmgt/src/Api/Api.csproj`
  - Environment: Cosmos container = `customerrelationships`
  - Dependencies: cosmos-emulator, rabbitmq
  - Networks: riskinsure

- **crmgt-endpoint service** (NEW):
  - Build context: `services/customerrelationshipsmgt/src/Endpoint.In/Endpoint.In.csproj`
  - Environment: Cosmos container = `customerrelationships`, NServiceBus enabled
  - Dependencies: cosmos-emulator, rabbitmq
  - Networks: riskinsure (no exposed ports - internal messaging only)

---

## 🔍 Verification Checklist

### Pre-Deployment Verification (Local Development)

- [ ] **Code exists**: Verify `services/customerrelationshipsmgt/src/` has Api, Domain, Infrastructure, Endpoint.In projects
- [ ] **Dockerfiles present**: Confirm `services/customerrelationshipsmgt/src/Api/Dockerfile` and `Endpoint.In/Dockerfile` exist
- [ ] **Build locally**:
  ```bash
  dotnet build RiskInsure.slnx
  ```
- [ ] **Docker Compose test**:
  ```bash
  cd RiskInsure
  docker compose -f docker-compose.domain.yml up crmgt-api crmgt-endpoint
  ```
  Expected: Both services start without errors and connect to cosmos-emulator and rabbitmq

### Local API Verification

- [ ] **Health check**: `curl http://localhost:7083/api/relationships/health` returns HTTP 200
- [ ] **Cosmos container**: Query `customerrelationships` in Cosmos Emulator Data Explorer
- [ ] **Service Bus**: Verify queues created: `RiskInsure.CustomerRelationshipsMgt.Endpoint` and `riskinsure.customerrelationshipsmgt.api`

### Terraform Validation

- [ ] **Syntax check**:
  ```bash
  cd platform/infra/services
  terraform validate
  ```
- [ ] **Dev plan**:
  ```bash
  terraform plan -var-file=dev.tfvars
  ```
  Expected: Plan shows +2 azurerm_container_app, +1 cosmosdb_sql_container, +6 servicebus_queue/topic resources

### CI/CD Pipeline Verification

- [ ] **Services list update**: Confirm CI detects customerrelationshipsmgt in changed files
  - Push change to `services/customerrelationshipsmgt/` directory
  - Verify CI workflow shows customerrelationshipsmgt in service list
- [ ] **Image build**: Confirm images appear in ACR after successful CI run
  - Check: `riskinsuregdevacr.azurecr.io/crmgt-api:${commit-sha}`
  - Check: `riskinsuregdevacr.azurecr.io/crmgt-endpoint:${commit-sha}`

### Dev Deployment Verification

- [ ] **Terraform apply** (after CI success):
  ```bash
  cd platform/infra/services
  terraform apply -var-file=dev.tfvars
  ```
- [ ] **Container Apps status**: Azure Portal → Container Apps → crmgt-api/crmgt-endpoint
  - Status: Running ✓
  - Latest revision: Active ✓
  - Replicas: Running (1 minimum replica) ✓
- [ ] **Metrics in Application Insights**:
  - Search for "RiskInsure.CustomerRelationshipsMgt" namespace
  - Verify startup logs, no errors ✓
- [ ] **API endpoint**: Test via public FQDN
  ```bash
  curl https://{crmgt-api-fqdn}.azurecontainerapps.io/api/relationships/health
  ```
  Expected: HTTP 200 with health status ✓

### Service Bus Integration Verification

- [ ] **Queues created**: Azure Portal → Service Bus Namespace → Queues
  - `RiskInsure.CustomerRelationshipsMgt.Endpoint` (Active, message count = 0) ✓
  - `riskinsure.customerrelationshipsmgt.api` (Active, message count = 0) ✓
- [ ] **Topics created**: Check 4 topics
  - RelationshipCreated ✓
  - RelationshipInformationUpdated ✓
  - ContactInformationChanged ✓
  - RelationshipClosed ✓

### Data Isolation Verification

- [ ] **Cosmos containers separate**: Query both containers
  ```sql
  -- Customer container
  SELECT * FROM customer c
  
  -- CRM container
  SELECT * FROM customerrelationships cr
  ```
  Expected: No cross-contamination, separate document counts ✓

### Functional Testing

- [ ] **API health check**: GET `/api/relationships/health` → 200
- [ ] **Create relationship**: POST `/api/relationships` with valid request
  - Expected: 201 Created, relationshipId returned
  - Verify: Document created in Cosmos
  - Verify: RelationshipCreated event published ✓
- [ ] **Query relationships**: GET `/api/relationships?customerId={id}` → 200
- [ ] **Update relationship**: PUT `/api/relationships/{id}` with valid request
  - Expected: 200 OK, RelationshipInformationUpdated event published ✓


### Logging & Monitoring

- [ ] **Correlation IDs**: Check Application Insights logs include relationshipId
- [ ] **Structured logging**: Verify logs follow standard format
  - Timestamp, LogLevel, ProcessingUnitId, EntityId, Message ✓
- [ ] **No errors in logs**: Search Application Insights for ERROR level logs related to crmgt ✓

---

## 📊 Traffic Cutover Strategy (Post-Deployment)

### Phase 1: Smoke Testing (Day 1)
- Route internal/test traffic to crmgt-api
- Production load still 100% on customer-api
- Success criteria: Zero errors, health checks passing

### Phase 2: Canary Deployment (Day 1-2)
- Route 10% of create/update traffic to crmgt-api via routing layer
- Monitor error rates, latency, Cosmos RU consumption
- Success criteria: Error rate < customer baseline, latency similar

### Phase 3: Progressive Rollout (Days 3-7)
- Gradually increase traffic: 25% → 50% → 75% → 90% → 100%
- 1-2 days per phase, monitoring metrics continuously
- Success criteria: All metrics align with customer-app baseline

### Phase 4: Full Cutover (Day 7)
- Route 100% traffic to crmgt-api
- Keep customer-app running (min_replicas=1 for quick rollback)
- Success criteria: 7 consecutive days with <0.5% error rate

### Phase 5: Customer Retirement (Day 14+)
- After 2 weeks of stable crmgt operation
- Scale customer-app to 0 replicas
- Archive customer Cosmos container
- Remove customer-app.tf from Terraform after 30-90 day retention

---

## 🚨 Rollback Procedures

**If issues detected at any phase**:

1. **Traffic routing**: Revert routing percentage to previous stable level
2. **Scale up customer-app**: If scaled down
3. **Terraform state**: Remains unchanged (only routing layer modified)
4. **Verify customer-app**: Confirm health before full rollback

**Automatic rollback triggers**:
- Error rate > 2%
- p99 latency > 1 second
- Cosmos throttling (429s) > 5%
- Dead-letter queue depth > 50

---

## 📝 Post-Deployment Configuration Checks

### Application Settings (appsettings.json)

Verify in service code (should be auto-configured):
- `CosmosDb__DatabaseName`: "RiskInsure"
- `CosmosDb__ContainerName`: "customerrelationships"
- `AzureServiceBus__FullyQualifiedNamespace`: Shared Service Bus namespace
- NServiceBus endpoint name: "RiskInsure.CustomerRelationshipsMgt.Endpoint"

### Key Vault Secrets

Should already exist (used by both services):
- `CosmosDbConnectionString` (shared)
- `ServiceBusConnectionString` (shared)

### Managed Identity Permissions

Shared identity already assigned:
- Cosmos DB: Data Contributor role
- Service Bus: Azure Service Bus Data Owner role

---

## 📈 Success Metrics (Parallel Run)

Compare crmgt vs customer during traffic cutover:

| Metric | Target | Monitoring Tool |
|--------|--------|-----------------|
| p95 Latency | <200ms (customer baseline) | App Insights |
| p99 Latency | <500ms | App Insights |
| Error Rate | <0.5% (customer baseline) | App Insights |
| Cosmos RU/s | Proportional to traffic % | Cosmos Metrics |
| Cosmos Throttling (429s) | <0.1% | Cosmos Diagnostics |
| Message Throughput | Proportional to traffic % | Service Bus Metrics |
| Queue Dead-Letters | 0 | Service Bus Queues |

---

## 🔗 Related Files & Resources

### Infrastructure Files Modified
- [crmgt-app.tf](../../platform/infra/services/crmgt-app.tf) - NEW
- [variables.tf](../../platform/infra/services/variables.tf) - +customerrelationshipsmgt entry
- [cosmosdb.tf](../../platform/infra/shared-services/cosmosdb.tf) - +customerrelationships containers
- [servicebus.tf](../../platform/infra/shared-services/servicebus.tf) - +crmgt queues and topics

### CI/CD Pipelines Modified
- [ci-build-services.yml](../../.github/workflows/ci-build-services.yml) - +customerrelationshipsmgt to ALL_SERVICES
- [cd-services-dev.yml](../../.github/workflows/cd-services-dev.yml) - +customerrelationshipsmgt to ALL_SERVICES

### Docker Configuration
- [docker-compose.domain.yml](../../docker-compose.domain.yml) - +crmgt-api/endpoint services

### Service Code (Completed)
- `services/customerrelationshipsmgt/src/Api/` - HTTP endpoints
- `services/customerrelationshipsmgt/src/Endpoint.In/` - NServiceBus handlers
- `services/customerrelationshipsmgt/src/Domain/` - Business logic
- `services/customerrelationshipsmgt/src/Infrastructure/` - Cosmos/NServiceBus config

---

## ⚠️ Important Notes

1. **Service code validation**: Confirm service code is complete and Dockerfiles exist before deploying
2. **Port conflicts**: crmgt-api uses port **7083** in local Docker Compose
3. **Partition key**: Uses `/customerId` matching Customer service pattern; can be changed to `/relationshipId` if domain requires
4. **Event names**: Use renamed contracts from service code (RelationshipCreated, etc.)
5. **Identity format**: Service code should use "CRM-{timestamp}" format to avoid collisions with customer "CUST-{timestamp}"

---

## ✅ Final Checklist Before Go-Live

- [ ] Terraform plan reviewed and approved
- [ ] Service code builds successfully (`dotnet build`)
- [ ] Local Docker Compose deployment successful
- [ ] CI/CD pipeline validates and builds images
- [ ] Dev deployment creates resources without errors
- [ ] API health checks responding
- [ ] Cosmos containers accessible
- [ ] Service Bus queues/topics created
- [ ] Application Insights receiving telemetry
- [ ] Team trained on monitoring and rollback procedures
- [ ] Communication plan established with stakeholders
- [ ] Success metrics baseline established from customer-app
- [ ] Runbook prepared for go-live

---

Generated: March 9, 2026 | Implementation: Complete ✅ | Next: Service validation and testing
