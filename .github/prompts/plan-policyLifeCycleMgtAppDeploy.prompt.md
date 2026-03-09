## Plan: Deploy PolicyLifeCycleMgt (policylifecyclemgt-app) with Zero Downtime

Clone Policy's Terraform infrastructure to deploy PolicyLifeCycleMgt (policylifecyclemgt-app) as a parallel service. The service code is complete; this plan focuses on infrastructure provisioning, CI/CD integration, and safe traffic cutover while maintaining Policy until retirement.

**TL;DR**: Create Terraform configuration for policylifecyclemgt-app Container Apps (API + Endpoint), add to CI/CD pipelines, deploy alongside policy-app, verify functionality with parallel traffic, then gradually cut over. Both services will coexist during the transition using separate Cosmos containers (policy vs policylifecycle) and separate sequential numbering (KWG vs LCM), enabling zero-downtime migration. Coordinate cutover with Rating & Underwriting (QuoteAccepted routing) and Billing (PolicyIssued/LifeCycleIssued subscription).

**Steps**

1. **Create Terraform configuration for policylifecyclemgt Container Apps**
   - Clone platform/infra/services/policy-app.tf to policylifecyclemgt-app.tf (base template with API and Endpoint resources)
   - Update resource names: azurerm_container_app.policylifecyclemgt_api and azurerm_container_app.policylifecyclemgt_endpoint
   - Change container app names to policylifecyclemgt-api and policylifecyclemgt-endpoint
   - Update Docker image references to use policylifecyclemgt-api:${var.image_tag} and policylifecyclemgt-endpoint:${var.image_tag}
   - Replace environment variable CosmosDb__ContainerName from "policy" to "policylifecycle"
   - Update NServiceBus endpoint queue references: RiskInsure.PolicyLifeCycleMgt.Endpoint and RiskInsure.PolicyLifeCycleMgt.Api (send-only)
   - Keep identical structure: managed identity (apps_shared_identity_id), Key Vault secrets (cosmos-connection-string, servicebus-connection-string), ingress on port 8080 (API only), Application Insights integration
   - Verify ASPNETCORE_URLS=http://+:8080 for API container

2. **Add policylifecyclemgt service configuration to Terraform variables**
   - Update platform/infra/services/variables.tf services map to add "policylifecyclemgt" entry (after policy definition)
   - Configure with same baseline as policy: api (cpu = 0.25, memory = "0.5Gi", min_replicas = 1, max_replicas = 10, container_name = "policylifecyclemgt"), endpoint (cpu = 0.25, memory = "0.5Gi", min_replicas = 1, max_replicas = 5, container_name = "policylifecyclemgt")
   - Ensure both api and endpoint components defined with enabled = true

3. **Create Cosmos DB containers for policylifecycle**
   - Update platform/infra/shared-services/cosmosdb.tf to add new container resources
   - Add azurerm_cosmosdb_sql_container.policylifecycle block for main data container
   - Configure with: partition_key_paths = ["/lifeCycleId"] (renamed from /policyId per domain decision), throughput = var.cosmosdb_throughput, indexing_policy consistent with includes all paths (/*)
   - Add azurerm_cosmosdb_sql_container.policylifecycle_sagas block for NServiceBus saga storage
   - Configure sagas container with same partition key and indexing policy
   - Add outputs for container IDs if needed for references

4. **Update Service Bus configuration for policylifecyclemgt queues and topics**
   - Update platform/infra/shared-services/servicebus.tf to add queues and topics
   - Add azurerm_servicebus_queue.policylifecyclemgt_endpoint with name "RiskInsure.PolicyLifeCycleMgt.Endpoint"
   - Add azurerm_servicebus_queue.policylifecyclemgt_api with name "RiskInsure.PolicyLifeCycleMgt.Api" (send-only endpoint, may not be needed if using "riskinsure.policylifecyclemgt.api" pattern)
   - Add azurerm_servicebus_topic resources for 3 internal domain events: LifeCycleInitiated (formerly PolicyBound), LifeCycleCancelled (formerly PolicyCancelled), LifeCycleReinstated (formerly PolicyReinstated)
   - Note: PolicyIssued event remains in PublicContracts - decision needed on whether to publish as "PolicyIssued" (backward compat) or "LifeCycleIssued" (new event name)
   - Configure max_size_in_megabytes, max_message_size_in_kilobytes, enable_partitioning, duplicate_detection_history_time_window consistent with policy patterns
   - Add subscription to QuoteAccepted topic for RiskInsure.PolicyLifeCycleMgt.Endpoint during parallel run

5. **Update environment-specific variable files**
   - Add policylifecyclemgt configuration to platform/infra/services/dev.tfvars (same structure as policy entry)
   - Add matching configuration to platform/infra/services/prod.tfvars
   - Consider starting with conservative replica counts (min=1, max=5 for API, max=3 for endpoint) for initial deployment, can scale up during cutover
   - Verify image_tag variable points to latest (dev) or specific version (prod)
   - Ensure ingress_external_enabled=true for API, ingress_allow_insecure=true (dev) or false (prod)

6. **Update CI/CD pipelines for Docker image builds**
   - Modify .github/workflows/ci-build-services.yml: add "policylifecyclemgt" to ALL_SERVICES array (currently line 68)
   - Update corresponding CD workflow .github/workflows/cd-services-dev.yml to include policylifecyclemgt in deployment targets
   - Update .github/workflows/cd-services-prod.yml similarly for production deployments
   - Verify build matrix will include Dockerfiles at services/policylifecyclemgt/src/Api/Dockerfile and services/policylifecyclemgt/src/Endpoint.In/Dockerfile
   - Ensure Dockerfile context includes LifeCycleNumberGenerator for sequential numbering (LCM-{year}-{sequence})

7. **Add to local Docker Compose for development**
   - Update docker-compose.domain.yml to add policylifecyclemgt-api and policylifecyclemgt-endpoint services (after policy services around line 225+)
   - Configure ports: use 7079:8080 for policylifecyclemgt-api (avoiding conflicts with policy:7077, customer:7073, billing:7071, crmgt:7077)
   - Set environment variables: CosmosDb__ContainerName=policylifecycle, CosmosDb__DatabaseName=RiskInsure, Messaging__MessageBroker=${MESSAGE_BROKER}
   - Set NServiceBus endpoint names: RiskInsure.PolicyLifeCycleMgt.Api (send-only) and RiskInsure.PolicyLifeCycleMgt.Endpoint
   - Use same Dockerfile patterns: platform/templates/Dockerfile.api.compose and Dockerfile.endpoint.compose with PROJECT_PATH pointing to policylifecyclemgt projects (services/policylifecyclemgt/src/Api/Api.csproj and services/policylifecyclemgt/src/Endpoint.In/Endpoint.In.csproj)
   - Reuse same infrastructure dependencies: cosmos-emulator, rabbitmq, License.xml volume mount
   - Add IMPORT_SSL_CERTS_FOR=cosmosdb for API container to trust Cosmos Emulator certificate

8. **Initial deployment verification (parallel run phase)**
   - Trigger CI build workflow to build and push policylifecyclemgt images to ACR (riskinsuredevacr for dev, riskinsureprodacr for prod)
   - Apply Terraform changes in order: terraform init (if new modules), terraform plan -var-file=dev.tfvars, review outputs (should show +2 azurerm_container_app, +2 azurerm_cosmosdb_sql_container, +N azurerm_servicebus_* resources), then terraform apply -var-file=dev.tfvars in dev environment
   - Verify both policylifecyclemgt-api and policylifecyclemgt-endpoint Container Apps start successfully via Azure Portal (Container Apps → policylifecyclemgt-api/policylifecyclemgt-endpoint → Metrics → Replica count > 0)
   - Check Application Insights for startup logs: search for "RiskInsure.PolicyLifeCycleMgt" namespace, verify no errors, confirm LifeCycleNumberGenerator initialization logs
   - Test API endpoints directly via policylifecyclemgt-api ingress URL (external FQDN): GET /api/lifecycles/health should return HTTP 200
   - Confirm Cosmos DB containers "policylifecycle" and "policylifecycle-sagas" created and accessible (query via Data Explorer, verify partition key /lifeCycleId)
   - Verify NServiceBus endpoint RiskInsure.PolicyLifeCycleMgt.Endpoint appears in Service Bus namespace → Queues, subscription count > 0 (subscribed to QuoteAccepted)
   - Verify sequential number generation isolation: confirm LifeCycleNumberCounter-{year} document created in policylifecycle container, separate from PolicyNumberCounter-{year} in policy container

9. **Cross-service integration coordination**
   - **Rating & Underwriting coordination**: Implement NServiceBus routing to gradually shift QuoteAccepted message flow from Policy to PolicyLifeCycleMgt (start with 0%, ramp to 100%)
   - **Billing service coordination**: 
     - Option A (backward compatible): PolicyLifeCycleMgt publishes "PolicyIssued" event (same event name as Policy) during transition, billing service doesn't need changes
     - Option B (new event): PolicyLifeCycleMgt publishes "LifeCycleIssued" event, billing service subscribes to both PolicyIssued and LifeCycleIssued during transition
     - Recommendation: Use Option A initially to minimize downstream changes, switch to Option B post-cutover if domain clarity needed
   - Verify no other services depend on Policy internal events (PolicyBound, PolicyCancelled, PolicyReinstated) - these are domain-internal events that don't cross service boundaries

10. **Traffic routing and gradual cutover strategy**
    - **Phase 0 - Pre-deployment**: Capture baseline metrics from policy-app (quote acceptance rate, policy issuance time, premium calculation accuracy, cancellation/reinstatement success rates, sequential numbering continuity, error rates, p95/p99 latency)
    - **Phase 1 - Smoke testing**: Deploy policylifecyclemgt-app with 0% production traffic, route internal/test QuoteAccepted messages only, validate end-to-end flow (QuoteAccepted → LifeCycle creation → PolicyIssued → Billing account creation)
    - **Phase 2 - Canary**: Use NServiceBus message routing to route 10% of QuoteAccepted messages to PolicyLifeCycleMgt, monitor error rates, latency, Cosmos RU consumption, validation failures, sequential numbering gaps (verify LCM-2026-000001, 000002 incrementing correctly)
    - **Phase 3 - Progressive rollout**: Increase traffic percentage (25% → 50% → 75% → 90% → 100%) over monitoring intervals (e.g., 2-3 days each depending on volume and confidence)
    - **Phase 4 - Full cutover**: Route 100% of QuoteAccepted messages to policylifecyclemgt-endpoint, keep policy-app running but idle (min_replicas=1, max_replicas=1) for quick rollback
    - **Phase 5 - Policy retirement**: After 2-3 weeks of stable policylifecyclemgt operation with zero issues, scale policy-app to 0 replicas, archive policy Cosmos container data, remove policy-app.tf from Terraform after data retention period (30-90 days)

11. **Monitoring and rollback preparation**
    - Define success metrics: p95 latency <300ms (policy API baseline), p99 latency <800ms, error rate <0.5%, Cosmos throttling (429s) <0.1%, sequential numbering gaps = 0, premium calculation accuracy = 100% match, cancellation refund accuracy = 100% match
    - Set up Azure Monitor alerts for policylifecyclemgt-api and policylifecyclemgt-endpoint: high error rates (>1%), container restarts (>2/hour), health check failures, Cosmos throttling, Service Bus dead-letter queue depth (>10), sequential number generation failures, premium calculation errors
    - Document rollback procedure: (1) adjust NServiceBus routing percentage to previous level or 0%, (2) scale up policy-app if scaled down, (3) verify policy-app health and quote processing resumes, (4) Terraform state remains unchanged during routing shifts (only NServiceBus routing config modified), (5) investigate policylifecyclemgt issues before re-attempting ramp
    - Capture comparison metrics: Track dual dashboards comparing Policy vs PolicyLifeCycleMgt for: quote acceptance throughput, policy issuance success rate, API response times per endpoint, Cosmos RU/s consumption, sequential numbering patterns (KWG vs LCM), cancellation processing time, unearned premium calculation correctness, downstream billing account creation success rates
    - Set up alerts for data divergence: Compare policy counts (policies created, issued, cancelled, reinstated per hour) between Policy and PolicyLifeCycleMgt during canary/progressive phases - should be proportional to traffic split percentage

**Relevant files**

**Terraform Infrastructure**:
- platform/infra/services/policy-app.tf — Source template with API and Endpoint Container App resources, scaling config, environment variables
- platform/infra/services/policylifecyclemgt-app.tf — New file to create (clone of policy-app.tf with renamed resources)
- platform/infra/services/variables.tf — Add "policylifecyclemgt" entry to services map
- platform/infra/services/dev.tfvars — Add policylifecyclemgt configuration
- platform/infra/services/prod.tfvars — Add policylifecyclemgt configuration
- platform/infra/shared-services/cosmosdb.tf — Add azurerm_cosmosdb_sql_container.policylifecycle and policylifecycle_sagas (2 containers)
- platform/infra/shared-services/servicebus.tf — Add policylifecyclemgt queues and topics for NServiceBus

**CI/CD Pipelines**:
- .github/workflows/ci-build-services.yml — Add "policylifecyclemgt" to ALL_SERVICES array (line 68)
- .github/workflows/cd-services-dev.yml — Deployment automation (will auto-detect new service)
- .github/workflows/cd-services-prod.yml — Production deployment workflow

**Docker Configuration**:
- docker-compose.domain.yml — Add policylifecyclemgt-api and policylifecyclemgt-endpoint services (line 225+)
- services/policylifecyclemgt/src/Api/Dockerfile — Docker build definition (should exist from service code completion)
- services/policylifecyclemgt/src/Endpoint.In/Dockerfile — Docker build definition (should exist)

**Service Code (already completed)**:
- services/policylifecyclemgt/src/Api/ — HTTP API with API port config in appsettings.json
- services/policylifecyclemgt/src/Endpoint.In/ — NServiceBus message handler host (QuoteAcceptedLifeCycleHandler)
- services/policylifecyclemgt/src/Domain/ — Business logic, contracts, repositories, LifeCycleNumberGenerator
- services/policylifecyclemgt/src/Infrastructure/ — Cosmos and NServiceBus configuration

**Cross-Service Integration**:
- platform/RiskInsure.PublicContracts/ — QuoteAccepted (consumed by both Policy and PolicyLifeCycleMgt during transition), PolicyIssued (produced by both)
- services/ratingandunderwriting/ — Source of QuoteAccepted events, needs routing configuration for gradual cutover
- services/billing/ — Consumer of PolicyIssued events, may need to subscribe to LifeCycleIssued if Option B chosen

**Verification**

1. Local verification: `docker compose -f docker-compose.domain.yml up policylifecyclemgt-api policylifecyclemgt-endpoint` → test API at http://localhost:7079/api/lifecycles/health returns HTTP 200
2. Cosmos DB local: Query policylifecycle container in Cosmos Emulator, verify separate from policy container, confirm partition key /lifeCycleId, verify LifeCycleNumberCounter-2026 document initialized
3. Sequential numbering test: Create multiple lifecycles locally, verify LCM-2026-000001, 000002, 000003 sequence, confirm no gaps, validate counter document increments correctly with optimistic concurrency (ETag-based)
4. CI build: Push commit with updated workflows and Terraform → verify policylifecyclemgt-api and policylifecyclemgt-endpoint images appear in ACR (riskinsuredevacr)
5. Terraform plan: `cd platform/infra/services && terraform plan -var-file=dev.tfvars` → review plan shows +2 azurerm_container_app, +2 azurerm_cosmosdb_sql_container, +N azurerm_servicebus_* resources, no changes to policy-app resources
6. Terraform apply: `terraform apply -var-file=dev.tfvars` → verify Container Apps in Azure Portal under resource group CAIS-010-RiskInsure
7. Health checks: `curl https://{policylifecyclemgt-api-fqdn}.azurecontainerapps.io/api/lifecycles/health` returns HTTP 200 with "Healthy" status
8. NServiceBus connectivity: Check Service Bus namespace → Queues → RiskInsure.PolicyLifeCycleMgt.Endpoint shows Active status, message count = 0, dead-letter count = 0
9. Service Bus subscriptions: Verify QuoteAccepted topic has subscription for RiskInsure.PolicyLifeCycleMgt.Endpoint, verify 3 topic resources created for internal events (LifeCycleInitiated, LifeCycleCancelled, LifeCycleReinstated)
10. API functional test (end-to-end): Publish QuoteAccepted message to Service Bus → verify policylifecyclemgt-endpoint processes message → verify lifecycle created in policylifecycle container with LCM-{year}-{sequence} identifier → verify PolicyIssued event published → verify billing service receives event and creates account
11. Premium calculation test: Create lifecycle with 12-month term, cancel mid-term (6 months remaining), verify unearned premium = 50% of total premium (rounded to 2 decimals), compare with Policy service calculation for same inputs
12. State machine test: Verify lifecycle status transitions: Bound (creation) → Issued (IssuePolicyAsync) → Active (effective date reached) → Cancelled (CancelPolicyAsync) → Reinstated (ReinstatePolicyAsync), validate state change events published correctly
13. Data isolation: Query both "policy" and "policylifecycle" Cosmos containers → confirm separate data, no cross-contamination, separate sequential numbering counters (PolicyNumberCounter vs LifeCycleNumberCounter)
14. Cutover success: Compare Application Insights metrics between policy and policylifecyclemgt during parallel run → validate latency (should be similar), throughput (should match traffic % split), error rates (should be ≤ policy baseline), sequential numbering continuity (no gaps in either service)
15. Log integrity: Search Application Insights logs for correlation identifiers (lifeCycleId), verify structured logging format matches standards, check for sequential number generation audit trail (counter document reads, ETag conflicts, retry attempts), validate no configuration errors or connection failures
16. Integration test with billing: Verify billing service receives PolicyIssued events from policylifecyclemgt-endpoint and creates billing accounts correctly, compare account creation success rate with policy-app baseline

**Decisions**

- **Cosmos containers**: Use separate "policylifecycle" and "policylifecycle-sagas" containers (no shared data with Policy service) for complete service isolation
- **Partition key**: Change from "/policyId" to "/lifeCycleId" for domain clarity and consistency with renamed domain model (requires coordinated change in service code + Terraform)
- **Sequential numbering**: Change from "KWG-{year}-{sequence:D6}" to "LCM-{year}-{sequence:D6}" (LifeCycle Management prefix), start fresh at LCM-2026-000001 for clear service ownership identification
- **Counter document**: Rename from "PolicyNumberCounter-{year}" to "LifeCycleNumberCounter-{year}" for domain consistency
- **NServiceBus endpoints**: New endpoint names RiskInsure.PolicyLifeCycleMgt.Endpoint and RiskInsure.PolicyLifeCycleMgt.Api (no routing conflicts with policy)
- **API routes**: Prefix changed from /api/policies to /api/lifecycles as per domain rename in service code
- **Event naming strategy**: 
  - Internal events renamed: PolicyBound → LifeCycleInitiated, PolicyCancelled → LifeCycleCancelled, PolicyReinstated → LifeCycleReinstated
  - PublicContracts event: Keep "PolicyIssued" name initially for backward compatibility with billing service (Option A), consider renaming to "LifeCycleIssued" post-cutover if domain clarity needed
- **Status values**: Keep Bound/Issued/Active/Cancelled/Expired/Lapsed/Reinstated terminology (no rename) to maintain business domain language consistency
- **Deployment strategy**: Parallel run with gradual traffic shift via NServiceBus message routing (not feature flag in code, but routing configuration at messaging layer)
- **Replica configuration**: Start with same capacity as policy (API: 1-10 dev, 1-10 prod; Endpoint: 1-5 dev, 1-5 prod) to handle equivalent load
- **Scaling**: Use KEDA auto-scaling based on Service Bus queue depth (enable_keda_scaling = true) for Endpoint, CPU-based auto-scaling for API
- **Rollback approach**: NServiceBus routing percentage reversible without code changes; policy-app remains deployed until policylifecyclemgt proven stable for 2-3 weeks
- **Ports**: Docker Compose local development uses 7079 for policylifecyclemgt-api (avoiding conflicts with policy:7077, customer:7073, billing:7071, crmgt:7077)

**Dependencies and Integration Points**

- **Shared infrastructure**: Uses same Key Vault secrets (CosmosDbConnectionString, ServiceBusConnectionString), managed identity (apps_shared_identity_id), Application Insights, Log Analytics workspace
- **Service Bus topology**: New queues and topic subscriptions created automatically for RiskInsure.PolicyLifeCycleMgt.Endpoint when first deployed
- **QuoteAccepted routing**: Rating & Underwriting must implement NServiceBus routing configuration to gradually shift QuoteAccepted messages from Policy to PolicyLifeCycleMgt (coordinate with R&U team for routing percentage changes during each cutover phase)
- **PolicyIssued consumption**: Billing service depends on PolicyIssued events - during transition, both Policy and PolicyLifeCycleMgt will publish PolicyIssued (same event name) to avoid breaking billing service integration. Post-cutover, consider renaming to LifeCycleIssued if domain clarity needed
- **API Gateway/Front Door**: If using centralized routing (Azure Front Door, API Management, or Application Gateway), update upstream routing rules to include policylifecyclemgt-api FQDN
- **Foundation layer**: Depends on foundation.tfstate outputs (resource_group_name, acr_login_server, log_analytics_workspace_id, application_insights_connection_string)
- **Shared services layer**: Depends on shared-services.tfstate outputs (apps_shared_identity_id, cosmosdb_endpoint, servicebus_namespace_fqdn)

**Risk Mitigations**

- **Config drift**: Both policy-app.tf and policylifecyclemgt-app.tf share identical structure except naming → use `diff -u policy-app.tf policylifecyclemgt-app.tf` to verify only expected differences before apply
- **Cosmos provisioning**: Ensure sufficient RU/s provisioned for parallel containers (policy + policylifecycle + both sagas containers) to avoid throttling during parallel run; consider temporarily increasing throughput from baseline 400 RU/s to 800-1000 RU/s during cutover peak
- **Cosmos partition key**: Renaming from /policyId to /lifeCycleId requires synchronized change in both Terraform (partition_key_paths) and service code (Policy model with [JsonPropertyName("lifeCycleId")]) — validate in dev first, coordination critical
- **Sequential numbering isolation**: Critical that LifeCycleNumberGenerator and PolicyNumberGenerator use separate counter documents (LifeCycleNumberCounter vs PolicyNumberCounter) to avoid conflicts and ensure independent sequences
- **Sequential numbering gaps**: Monitor for ETag conflicts during high-volume periods - LifeCycleNumberGenerator retries up to 5 times on conflict, but sustained high contention may cause gaps or errors. Consider increasing retry limit or implementing pessimistic locking if gaps detected
- **NServiceBus installers**: First deployment with EnableInstallers() may need elevated Service Bus permissions → verify Managed Identity has "Azure Service Bus Data Owner" role assignment on namespace
- **Image tag sync**: CI/CD must build both policy and policylifecyclemgt from same commit to maintain version parity during parallel run; verify image tags align in ACR
- **Premium calculation logic**: PolicyLifeCycleMgt must replicate exact Policy premium calculation (unearned premium = remaining days / total days × premium) to avoid customer refund discrepancies → add automated tests comparing calculations between services
- **State machine transitions**: Verify PolicyLifeCycleMgt implements identical state transition rules as Policy (e.g., can only cancel Active policies, can only reinstate Cancelled/Lapsed policies) → regression test all paths
- **QuoteAccepted idempotency**: Both Policy and PolicyLifeCycleMgt check GetByQuoteIdAsync before creating policy/lifecycle → during transition, ensure QuoteAccepted messages not duplicated (could create both policy and lifecycle from same quote if dual-published instead of routed)
- **Dead-letter queues**: Monitor Service Bus dead-letter queues for RiskInsure.PolicyLifeCycleMgt.Endpoint during initial deployment and cutover; high dead-letter counts indicate message processing failures requiring investigation
- **Billing integration**: Test billing service account creation from PolicyIssued events published by PolicyLifeCycleMgt → verify account creation success rate matches Policy baseline (>99.9%), no missing accounts or duplicates
- **Data migration**: Decision made to start fresh (no migration of existing Policy data to PolicyLifeCycleMgt) → communicate clearly to support teams which service owns which policies (Policy: KWG-*, PolicyLifeCycleMgt: LCM-*)

**Cutover Coordination**

- **Stakeholders**: DevOps (Terraform/CI/CD), Development (service code verification), QA (integration testing), Operations (monitoring alerts), Rating & Underwriting team (QuoteAccepted routing), Billing team (PolicyIssued subscription), Product Owner (business decision for go/no-go)
- **Communication plan**: Schedule cutover phases with advance notice (e.g., Phase 1 smoke testing 3 days, Phase 2 canary 10% for 2 days, Phase 3 progressive rollout 1 week total, Phase 4 full cutover with explicit approval), send status updates after each phase with metrics comparison
- **Rollback triggers**: 
  - Automatic rollback if: error rate >2%, p99 latency >1.5s, Cosmos throttling >5%, dead-letter queue depth >50, sequential numbering gap detected, premium calculation mismatch >$0.01
  - Manual rollback for: any data integrity issues, billing integration failures, customer-facing errors, support team escalations
- **Success criteria for each phase**:
  - Phase 1: Zero errors, API responds correctly, sequential numbering works, billing receives events
  - Phase 2: Error rate <0.5%, latency matches Policy baseline, no sequential gaps, billing account creation 100% success
  - Phase 3: Sustained performance at each percentage level (25%, 50%, 75%), no customer complaints, no support escalations
  - Phase 4: 7 days at 100% traffic with error rate <0.3%, p95 latency <300ms, zero Cosmos throttling, zero sequential gaps, zero customer refund calculation errors, zero billing integration failures
- **Coordination meetings**: Schedule daily stand-ups during canary/progressive phases to review metrics, discuss issues, make go/no-go decisions for next percentage ramp

**Post-Deployment Cleanup**

After successful cutover and stability period (2-3 weeks at 100% traffic to policylifecyclemgt-app):

1. Scale policy-app to min_replicas=0, max_replicas=0 in variables (soft retirement, policy-app.tf remains but containers stopped)
2. Monitor for 30-60 days to ensure no unexpected dependencies or support issues requiring policy-app reactivation
3. Archive policy Cosmos container: export all data to Azure Blob Storage for compliance retention (7 years for insurance policies), document export location and format
4. Retain PolicyNumberCounter documents: Keep final counter values (e.g., KWG-2026-123456) for audit trail and potential future reference
5. Remove policy-app.tf from Terraform configuration: Comment out first, then delete after final verification
6. Decommission policy Service Bus queues/topics: Verify no subscribers remain, delete RiskInsure.Policy.Endpoint queue and PolicyBound/PolicyCancelled/PolicyReinstated topics
7. Update documentation: Mark policy-app as deprecated, update architectural diagrams to show PolicyLifeCycleMgt as authoritative for policy lifecycle management
8. Remove policy-app from CI/CD pipelines: Remove "policy" from ALL_SERVICES array in ci-build-services.yml
9. Remove policy-app from docker-compose.domain.yml: Delete policy-api and policy-endpoint service definitions (local dev no longer needed)
10. Coordinate with Rating & Underwriting: Remove QuoteAccepted routing to Policy endpoint, keep only PolicyLifeCycleMgt routing
11. Coordinate with Billing: Remove PolicyIssued subscription from Policy (if dual-subscribed during transition), keep only PolicyLifeCycleMgt subscription

**Cross-Service Cutover Timeline Example**

Assuming 2-week gradual rollout:

- **Day 0**: Deploy policylifecyclemgt-app to dev, smoke test with internal traffic
- **Day 3**: Deploy to prod, Phase 1 smoke testing complete (0% production)
- **Day 5**: Phase 2 canary starts (10% QuoteAccepted routed to PolicyLifeCycleMgt)
- **Day 7**: Review canary metrics, increase to 25% if successful
- **Day 9**: Increase to 50% (this is the critical inflection point - 50% of policies now created in new service)
- **Day 11**: Increase to 75%
- **Day 13**: Increase to 90%
- **Day 14**: Full cutover to 100%, policy-app receives 0% traffic but remains running
- **Day 21-28**: Monitor stability at 100%, verify no issues
- **Day 28**: Scale policy-app to 0 replicas (soft retirement)
- **Day 58-88**: Archive policy container data, remove policy-app.tf from Terraform
