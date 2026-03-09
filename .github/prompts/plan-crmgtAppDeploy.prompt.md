## Plan: Deploy CustomerRelationshipsMgt (crmgt-app) with Zero Downtime

Clone Customer's Terraform infrastructure to deploy CustomerRelationshipsMgt (crmgt-app) as a parallel service. The service code is complete; this plan focuses on infrastructure provisioning, CI/CD integration, and safe traffic cutover while maintaining Customer until retirement.

**TL;DR**: Create Terraform configuration for crmgt-app Container Apps (API + Endpoint), add to CI/CD pipelines, deploy alongside customer-app, verify functionality with parallel traffic, then gradually cut over. Both services will coexist during the transition using separate Cosmos containers (customer vs customerrelationships), enabling zero-downtime migration.

**Steps**

1. **Create Terraform configuration for crmgt Container Apps**
   - Clone platform/infra/services/customer-app.tf to crmgt-app.tf (233 lines base template)
   - Update resource names: azurerm_container_app.crmgt_api and azurerm_container_app.crmgt_endpoint
   - Change container app names to crmgt-api and crmgt-endpoint
   - Update Docker image references to use crmgt-api:${var.image_tag} and crmgt-endpoint:${var.image_tag}
   - Replace environment variable CosmosDb__ContainerName from "customer" to "customerrelationships"
   - Keep identical structure: managed identity (apps_shared_identity_id), Key Vault secrets (CosmosDbConnectionString, ServiceBusConnectionString), ingress on port 8080 (API only), Application Insights integration
   - Verify NServiceBus endpoint queue name references: RiskInsure.CustomerRelationshipsMgt.Endpoint and riskinsure.customerrelationshipsmgt.api (send-only)

2. **Add crmgt service configuration to Terraform variables**
   - Update platform/infra/services/variables.tf services map to add "customerrelationshipsmgt" entry (after line 225)
   - Configure with same baseline as customer: api (cpu = 0.25, memory = "0.5Gi", min_replicas = 1, max_replicas = 10, container_name = "customerrelationshipsmgt"), endpoint (cpu = 0.25, memory = "0.5Gi", min_replicas = 1, max_replicas = 5, container_name = "customerrelationshipsmgt")
   - Ensure both api and endpoint components defined with enabled = true

3. **Create Cosmos DB container for customerrelationships**
   - Update platform/infra/shared-services/cosmosdb.tf to add new container resource
   - Add azurerm_cosmosdb_sql_container.customerrelationships block
   - Configure with same settings as customer container: partition_key_paths = ["/customerId"] (or rename to "/relationshipId" per domain decision), throughput = var.cosmosdb_throughput, indexing_policy consistent
   - Add output for container ID if needed for references

4. **Update Service Bus configuration for crmgt queues and topics**
   - Update platform/infra/shared-services/servicebus.tf to add queues and topics
   - Add azurerm_servicebus_queue.crmgt_endpoint with name "RiskInsure.CustomerRelationshipsMgt.Endpoint"
   - Add azurerm_servicebus_queue.crmgt_api with name "riskinsure.customerrelationshipsmgt.api" (send-only endpoint)
   - Add azurerm_servicebus_topic resources for 4 domain events: RelationshipCreated, RelationshipInformationUpdated, ContactInformationChanged, RelationshipClosed (following renamed event contracts from service code)
   - Configure max_size_in_megabytes, max_message_size_in_kilobytes, enable_partitioning consistent with customer patterns

5. **Update environment-specific variable files**
   - Add customerrelationshipsmgt configuration to platform/infra/services/dev.tfvars (same structure as customer entry)
   - Add matching configuration to platform/infra/services/prod.tfvars
   - Consider starting with conservative replica counts (min=1, max=5 for API, max=3 for endpoint) for initial deployment, can scale up during cutover
   - Verify image_tag variable points to latest (dev) or specific version (prod)

6. **Update CI/CD pipelines for Docker image builds**
   - Modify .github/workflows/ci-build-services.yml line 68: add "customerrelationshipsmgt" to ALL_SERVICES array
   - Update corresponding CD workflow .github/workflows/cd-services-dev.yml to include customerrelationshipsmgt in deployment targets
   - Verify build matrix will include Dockerfiles at services/customerrelationshipsmgt/src/Api/Dockerfile and services/customerrelationshipsmgt/src/Endpoint.In/Dockerfile
   - Update .github/workflows/cd-services-prod.yml similarly for production deployments

7. **Add to local Docker Compose for development**
   - Update docker-compose.domain.yml to add crmgt-api and crmgt-endpoint services
   - Configure ports: use 7077:8080 for crmgt-api (avoiding conflicts with customer:7073, billing:7071, policy:7079)
   - Set environment variable CosmosDb__ContainerName=customerrelationships
   - Set NServiceBus endpoint names: RiskInsure.CustomerRelationshipsMgt.Api (send-only) and RiskInsure.CustomerRelationshipsMgt.Endpoint
   - Use same Dockerfile patterns: platform/templates/Dockerfile.api.compose and Dockerfile.endpoint.compose with PROJECT_PATH pointing to crmgt projects (services/customerrelationshipsmgt/src/Api/Api.csproj and services/customerrelationshipsmgt/src/Endpoint.In/Endpoint.In.csproj)
   - Reuse same infrastructure dependencies: cosmos-emulator, rabbitmq, License.xml volume mount

8. **Initial deployment verification (parallel run phase)**
   - Trigger CI build workflow to build and push crmgt images to ACR (riskinsuregdevacr for dev, riskinsureprodacr for prod)
   - Apply Terraform changes in order: terraform init (if new modules), terraform plan -var-file=dev.tfvars, review outputs, then terraform apply -var-file=dev.tfvars in dev environment
   - Verify both crmgt-api and crmgt-endpoint Container Apps start successfully via Azure Portal (Container Apps → crmgt-api/crmgt-endpoint → Metrics)
   - Check Application Insights for startup logs: search for "RiskInsure.CustomerRelationshipsMgt" namespace, verify no errors
   - Test API endpoints directly via crmgt-api ingress URL (external FQDN): GET /api/relationships/health should return HTTP 200
   - Confirm Cosmos DB container "customerrelationships" created and accessible (query via Data Explorer)
   - Verify NServiceBus endpoint RiskInsure.CustomerRelationshipsMgt.Endpoint appears in Service Bus namespace → Queues, subscription count > 0

9. **Traffic routing and gradual cutover strategy**
   - **Phase 1 - Smoke testing**: Route internal/test traffic to crmgt-api while customer-api handles all production load (100% customer, 0% crmgt)
   - **Phase 2 - Canary**: Use Azure Front Door, API Management, or Application Gateway (if available) to route 10% of create/update traffic to crmgt-api, monitor error rates, latency, Cosmos RU consumption, validation failures
   - **Phase 3 - Progressive rollout**: Increase traffic percentage (25% → 50% → 75% → 90% → 100%) over monitoring intervals (e.g., 1-2 days each depending on volume)
   - **Phase 4 - Full cutover**: Route 100% traffic to crmgt-api, keep customer-app running but idle (min_replicas=1, max_replicas=1) for quick rollback
   - **Phase 5 - Customer retirement**: After 1-2 weeks of stable crmgt operation with zero issues, scale customer-app to 0 replicas, then remove customer-app.tf from Terraform after data retention period (30-90 days)

10. **Monitoring and rollback preparation**
    - Define success metrics: p95 latency <200ms (customer API baseline), p99 latency <500ms, error rate <0.5%, Cosmos throttling (429s) <0.1%, validation error rate similar to customer baseline
    - Set up Azure Monitor alerts for crmgt-api and crmgt-endpoint: high error rates (>1%), container restarts (>2/hour), health check failures, Cosmos throttling, Service Bus dead-letter queue depth (>10)
    - Document rollback procedure: (1) revert traffic routing percentage to previous level, (2) scale up customer-app if scaled down, (3) Terraform state remains unchanged during traffic shifts (only routing layer modified), (4) verify customer-app health before full rollback
    - Capture baseline metrics from customer-app before cutover begins: average response time per endpoint, error rates by error type, throughput (requests/min), Cosmos RU/s consumption, validation failure percentages

**Relevant files**

**Terraform Infrastructure**:
- platform/infra/services/customer-app.tf — Source template (233 lines) with API and Endpoint Container App resources
- platform/infra/services/crmgt-app.tf — New file to create (clone of customer-app.tf with renamed resources)
- platform/infra/services/variables.tf — Add "customerrelationshipsmgt" entry to services map (line 225+)
- platform/infra/services/dev.tfvars — Add customerrelationshipsmgt configuration
- platform/infra/services/prod.tfvars — Add customerrelationshipsmgt configuration
- platform/infra/shared-services/cosmosdb.tf — Add azurerm_cosmosdb_sql_container.customerrelationships (line 88+)
- platform/infra/shared-services/servicebus.tf — Add crmgt queues and topics for NServiceBus (line 71+)

**CI/CD Pipelines**:
- .github/workflows/ci-build-services.yml — Add "customerrelationshipsmgt" to ALL_SERVICES array (line 68)
- .github/workflows/cd-services-dev.yml — Deployment automation (will auto-detect new service)
- .github/workflows/cd-services-prod.yml — Production deployment workflow

**Docker Configuration**:
- docker-compose.domain.yml — Add crmgt-api and crmgt-endpoint services
- services/customerrelationshipsmgt/src/Api/Dockerfile — Docker build definition (should exist from service code completion)
- services/customerrelationshipsmgt/src/Endpoint.In/Dockerfile — Docker build definition (should exist)

**Service Code (already completed)**:
- services/customerrelationshipsmgt/src/Api/ — HTTP API with API port config in appsettings.json
- services/customerrelationshipsmgt/src/Endpoint.In/ — NServiceBus message handler host
- services/customerrelationshipsmgt/src/Domain/ — Business logic, contracts, repositories
- services/customerrelationshipsmgt/src/Infrastructure/ — Cosmos and NServiceBus configuration

**Verification**

1. Local verification: `docker compose -f docker-compose.domain.yml up crmgt-api crmgt-endpoint` → test API at http://localhost:7077/api/relationships/health returns HTTP 200
2. Cosmos DB local: Query customerrelationships container in Cosmos Emulator, verify separate from customer container, confirm partition key /customerId or /relationshipId
3. CI build: Push commit with updated workflows and Terraform → verify crmgt-api and crmgt-endpoint images appear in ACR (riskinsuregdevacr)
4. Terraform plan: `cd platform/infra/services && terraform plan -var-file=dev.tfvars` → review plan shows +2 azurerm_container_app, +1 azurerm_cosmosdb_sql_container, +N azurerm_servicebus_* resources
5. Terraform apply: `terraform apply -var-file=dev.tfvars` → verify Container Apps in Azure Portal under resource group CAIS-010-RiskInsure
6. Health checks: `curl https://{crmgt-api-fqdn}.azurecontainerapps.io/api/relationships/health` returns HTTP 200 with "Healthy" status
7. NServiceBus connectivity: Check Service Bus namespace → Queues → RiskInsure.CustomerRelationshipsMgt.Endpoint shows Active status, message count = 0, dead-letter count = 0
8. Service Bus subscriptions: Verify 4 topic subscriptions created for RelationshipCreated, RelationshipInformationUpdated, ContactInformationChanged, RelationshipClosed events
9. API functional test: POST /api/relationships with valid CreateRelationshipRequest → verify HTTP 201, response contains relationshipId (CRM-{timestamp} format), Cosmos document created, RelationshipCreated event published to Service Bus
10. Data isolation: Query both "customer" and "customerrelationships" Cosmos containers → confirm separate data, no cross-contamination
11. Cutover success: Compare Application Insights metrics between customer and crmgt during parallel run → validate latency (should be similar), throughput (should match traffic %), error rates (should be ≤ customer baseline)
12. Log integrity: Search Application Insights logs for correlation identifiers (relationshipId or crmgtId), verify structured logging format matches standards, check for any configuration errors or connection failures

**Decisions**

- **Cosmos container**: Use separate "customerrelationships" container (no shared data with Customer service) for complete service isolation
- **Partition key**: Retain "/customerId" initially for consistency (can rename to "/relationshipId" in step 3 if domain team decides, requires coordinated change in service code + Terraform)
- **NServiceBus endpoint**: New endpoint name RiskInsure.CustomerRelationshipsMgt.Endpoint (no routing conflicts with customer)
- **API routes**: Prefix changed from /api/customers to /api/relationships as per domain rename in service code
- **Event names**: RelationshipCreated, RelationshipInformationUpdated, ContactInformationChanged, RelationshipClosed (match renamed contracts in Domain layer)
- **Deployment strategy**: Parallel run with gradual traffic shift via Azure routing layer (not feature flag in code, but infrastructure-level load balancing)
- **Replica configuration**: Start with same capacity as customer (API: 1-10 dev, 1-5 prod; Endpoint: 1-5 dev, 1-3 prod) to handle equivalent load
- **Scaling**: Use KEDA auto-scaling based on Service Bus queue depth (enable_keda_scaling = true) for Endpoint, CPU-based auto-scaling for API
- **Rollback approach**: Infrastructure-level traffic routing reversible without code changes; customer-app remains deployed until crmgt proven stable for 1-2 weeks
- **Ports**: Docker Compose local development uses 7077 for crmgt-api (avoiding conflicts with customer:7073, billing:7071, policy:7079)

**Dependencies and Integration Points**

- **Shared infrastructure**: Uses same Key Vault secrets (CosmosDbConnectionString, ServiceBusConnectionString), managed identity (apps_shared_identity_id), Application Insights, Log Analytics workspace
- **Service Bus topology**: New queues and topic subscriptions created automatically for RiskInsure.CustomerRelationshipsMgt.Endpoint when first deployed
- **Cross-service impacts**: Verify no other services route commands/events to RiskInsure.Customer.Endpoint that should remain with customer (CRM service should be self-contained per architecture)
- **API Gateway/Front Door**: If using centralized routing (Azure Front Door, API Management, or Application Gateway), update upstream routing rules to include crmgt-api FQDN and traffic distribution percentages
- **Foundation layer**: Depends on foundation.tfstate outputs (resource_group_name, acr_login_server, log_analytics_workspace_id, application_insights_connection_string)
- **Shared services layer**: Depends on shared-services.tfstate outputs (apps_shared_identity_id, cosmosdb_endpoint, servicebus_namespace_fqdn)

**Risk Mitigations**

- **Config drift**: Both customer-app.tf and crmgt-app.tf share identical structure except naming → use `diff -u customer-app.tf crmgt-app.tf` to verify only expected differences before apply
- **Cosmos provisioning**: Ensure sufficient RU/s provisioned for parallel containers (customer + customerrelationships) to avoid throttling during parallel run; consider temporarily increasing throughput during cutover peak
- **Cosmos partition key**: If renaming from /customerId to /relationshipId, requires synchronized change in both Terraform (partition_key_paths) and service code (Customer model with [JsonPropertyName("relationshipId")]) — validate in dev first
- **NServiceBus installers**: First deployment with EnableInstallers() may need elevated Service Bus permissions → verify Managed Identity has "Azure Service Bus Data Owner" role assignment on namespace
- **Image tag sync**: CI/CD must build both customer and crmgt from same commit to maintain version parity during parallel run; verify image tags align in ACR
- **Validation logic**: Customer service has Email, ZipCode, and Age validation → verify crmgt service retains identical validation rules to avoid behavior differences during cutover
- **Identity generation**: Customer uses "CUST-{timestamp}" pattern → verify crmgt uses new pattern "CRM-{timestamp}" to avoid ID collisions and enable clear service ownership identification
- **Dead-letter queues**: Monitor Service Bus dead-letter queues for RiskInsure.CustomerRelationshipsMgt.Endpoint during initial deployment and cutover; high dead-letter counts indicate message processing failures requiring investigation
- **Cosmos uniqueness constraints**: Customer service validates email uniqueness via GetByEmailAsync cross-partition query → verify crmgt implements same constraint logic to prevent duplicate email registrations

**Cutover Coordination**

- **Stakeholders**: DevOps (Terraform/CI/CD), Development (service code verification), QA (integration testing), Operations (monitoring alerts), Product Owner (business decision for go/no-go)
- **Communication plan**: Schedule cutover phases with advance notice (e.g., Phase 2 canary after 3 days of smoke testing, Phase 3 progressive rollout over 1 week, Phase 4 full cutover requires explicit approval)
- **Rollback triggers**: Automatic rollback if error rate >2%, p99 latency >1s, Cosmos throttling >5%, dead-letter queue depth >50; manual rollback for any data integrity issues
- **Success criteria**: 7 days of 100% traffic to crmgt-app with error rate <0.5%, p95 latency <200ms, zero Cosmos throttling, zero dead-letter messages, no customer support escalations related to CRM functionality

**Post-Deployment Cleanup**

After successful cutover and stability period (1-2 weeks at 100% traffic to crmgt-app):

1. Scale customer-app to min_replicas=0, max_replicas=0 in variables (soft retirement)
2. Monitor for 30 days to ensure no unexpected dependencies
3. Remove customer-app.tf from Terraform configuration
4. Archive customer Cosmos container (export data, retain for compliance period)
5. Decommission customer Service Bus queues/topics (after verifying no subscribers)
6. Update documentation to reference crmgt-app as authoritative CustomerRelationshipsMgt service
7. Remove customer-app from CI/CD pipelines (ALL_SERVICES array)
8. Remove customer-app from docker-compose.domain.yml (local dev no longer needed)
