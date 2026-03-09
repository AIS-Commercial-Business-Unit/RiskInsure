## Plan: Deploy RiskRatingAndUnderwriting (rau-app) with Zero Downtime

Clone RatingAndUnderwriting's Terraform infrastructure to deploy RiskRatingAndUnderwriting (rau-app) as a parallel service. The service code is complete; this plan focuses on infrastructure provisioning, CI/CD integration, and safe traffic cutover while maintaining RatingAndUnderwriting until retirement.

**TL;DR**: Create Terraform configuration for rau-app Container Apps (API + Endpoint), add to CI/CD pipelines, deploy alongside ratingandunderwriting-app, verify functionality with parallel traffic, then gradually cut over. Both services will coexist during the transition using separate Cosmos containers (RatingUnderwriting vs RiskRatingAndUnderwriting), enabling zero-downtime migration. Decision made: use fresh Cosmos container "riskratingandunderwriting" per cloning strategy.

**Steps**

1. **Create Terraform configuration for rau Container Apps**
   - Clone platform/infra/services/ratingunderwriting-app.tf to riskratingandunderwriting-app.tf
   - Update resource names: azurerm_container_app.riskratingandunderwriting_api and azurerm_container_app.riskratingandunderwriting_endpoint
   - Change container app names to riskratingandunderwriting-api and riskratingandunderwriting-endpoint
   - Update Docker image references to use riskratingandunderwriting-api:${var.image_tag} and riskratingandunderwriting-endpoint:${var.image_tag}
   - Replace environment variable CosmosDb__ContainerName from "ratingunderwriting" to "riskratingandunderwriting"
   - Keep identical structure: managed identity, Key Vault secrets, ingress on port 8080, Application Insights integration, NServiceBus configuration

2. **Add rau service configuration to Terraform variables**
   - Update platform/infra/services/variables.tf services map to add "riskratingandunderwriting" entry
   - Configure with same baseline as ratingandunderwriting: cpu = 0.25, memory = "0.5Gi", min_replicas = 1, max_replicas = 10 (API) / 5 (Endpoint)
   - Set container_name = "riskratingandunderwriting" for Cosmos DB container reference
   - Ensure both api and endpoint components defined with enabled = true

3. **Update environment-specific variable files**
   - Add riskratingandunderwriting configuration to platform/infra/services/dev.tfvars
   - Add matching configuration to platform/infra/services/prod.tfvars
   - Consider starting with conservative replica counts (min=1, max=5) for initial deployment, can scale to 10 during cutover
   - May copy existing ratingandunderwriting entries or use defaults from variables.tf

4. **Update CI/CD pipelines for Docker image builds**
   - Modify .github/workflows/ci-build-services.yml: add "riskratingandunderwriting" to ALL_SERVICES array (around line 63)
   - Update corresponding .github/workflows/cd-services-dev.yml and cd-services-prod.yml to include riskratingandunderwriting in deployment targets
   - Verify build matrix will include Dockerfiles at services/riskratingandunderwriting/src/Api/Dockerfile and services/riskratingandunderwriting/src/Endpoint.In/Dockerfile
   - Update any service list documentation or scripts (scripts/*.ps1, smoke-test scripts)

5. **Add to local Docker Compose for development**
   - Update docker-compose.domain.yml to add riskratingandunderwriting-api and riskratingandunderwriting-endpoint services
   - Configure ports: use 7087:8080 for API (per user specification, avoiding conflicts with current services)
   - Set environment variable CosmosDb__ContainerName=riskratingandunderwriting
   - Use same Dockerfile patterns: platform/templates/Dockerfile.api.compose and Dockerfile.endpoint.compose with PROJECT_PATH pointing to rau projects
   - Reuse same infrastructure dependencies: cosmos-emulator, rabbitmq, License.xml volume
   - Consider port for Endpoint (if exposed locally, typically 7088 if following API:7087 pattern)

6. **Initial deployment verification (parallel run phase)**
   - Trigger CI build workflow to build and push rau images to ACR
   - Apply Terraform changes: terraform plan -var-file=dev.tfvars, review outputs, then terraform apply in dev environment
   - Verify both riskratingandunderwriting-api and riskratingandunderwriting-endpoint Container Apps start successfully via Azure Portal or CLI
   - Check Application Insights for startup logs and health check responses at /api/riskratingundewriting/health (or appropriate endpoint per service)
   - Test API endpoints directly via riskratingandunderwriting-api ingress URL (external FQDN)
   - Confirm Cosmos DB container "riskratingandunderwriting" created and accessible
   - Verify NServiceBus endpoint RiskInsure.RiskRatingAndUnderwriting.Endpoint appears in Service Bus subscriptions and queue/topic structure matches

7. **Traffic routing and gradual cutover strategy**
   - **Phase 1 - Shadow traffic**: Route test/validation traffic to rau-api while ratingandunderwriting-api handles production load
   - **Phase 2 - Canary**: Use Azure Front Door or API Gateway (if available) to route 10% traffic to rau-api, monitor error rates, latency, Cosmos RU consumption
   - **Phase 3 - Progressive rollout**: Increase traffic percentage (25% → 50% → 75% → 100%) over monitoring intervals (e.g., 1-2 days each)
   - **Phase 4 - Full cutover**: Route 100% traffic to rau-api, keep ratingandunderwriting-app running but idle for quick rollback
   - **Phase 5 - RatingAndUnderwriting retirement**: After 1-2 weeks of stable rau operation, scale ratingandunderwriting-app to 0 replicas, then remove from Terraform after data retention period

8. **Cross-service integration verification**
   - Verify downstream services (Policy, PolicyLifeCycleMgt) correctly routing to new RiskInsure.RiskRatingAndUnderwriting.Endpoint for QuoteAccepted messages
   - Check that billing/premium calculation services receive quotes from rau-endpoint with correct structure
   - Validate that all published events (quote acceptances, ratings, underwriting decisions) route to dependent services
   - Test message routing: publish test quote → verify processed by rau-endpoint → confirm events published to Service Bus

9. **Monitoring and rollback preparation**
   - Define success metrics: p95 latency <500ms, error rate <1%, Cosmos RU/s consumption within acceptable bounds
   - Set up Azure Monitor alerts for rau-api and rau-endpoint: high error rates, container restarts, health check failures
   - Document rollback procedure: revert traffic routing percentage, scale up ratingandunderwriting-app if needed, Terraform state remains unchanged during traffic shifts
   - Capture baseline metrics from ratingandunderwriting-app before cutover begins for comparison
   - Monitor underwriting decision logic consistency between old and new service (sample policies should have identical ratings/decisions)

10. **Terraform validation and cleanup**
   - Verify no resource name conflicts (check for duplicate container app names, Cosmos containers, Service Bus entities)
   - Test terraform destroy on both new resources independently, ensure clean state
   - Document container app resource IDs and monitor DNS CNAME propagation if traffic routing based on hostnames
   - Validate cost projections: two services running in parallel increases RU/s consumption (estimate 2x during transition)

11. **Post-cutover operations and retirement**
   - After 1-2 weeks of stable rau-app operation, disable deployments to ratingandunderwriting-app (mark as deprecated in CI/CD)
   - Scale ratingandunderwriting-app to 0 replicas in production, keep dev deployment for reference/rollback if needed
   - Update documentation to remove RatingAndUnderwriting service references, update architecture diagrams
   - Decommission ratingunderwriting Cosmos DB container after data retention compliance period
   - Archive ratingunderwriting-app.tf and remove from production Terraform configuration

**Relevant files**

**Source (RatingAndUnderwriting service)**:
- platform/infra/services/ratingunderwriting-app.tf — Complete Container Apps configuration (API + Endpoint), Cosmos container setup, Service Bus integration
- platform/infra/services/variables.tf — "ratingandunderwriting" service entry with defaults (cpu=0.25, memory=0.5Gi, max_replicas 10/5)
- platform/infra/services/dev.tfvars — Environment-specific overrides (if any)
- platform/infra/services/prod.tfvars — Production-specific overrides
- services/ratingandunderwriting/src/Api/Program.cs — NServiceBus send-only endpoint setup, Cosmos initialization, logging
- services/ratingandunderwriting/src/Endpoint.In/Program.cs — NServiceBus message handler host configuration
- docker-compose.domain.yml — Service configuration for local development (port 7079 for API)
- .github/workflows/ci-build-services.yml — CI build matrix including ratingandunderwriting
- .github/workflows/cd-services-dev.yml and cd-services-prod.yml — CD deployment workflows

**Target (new location)**:
- platform/infra/services/riskratingandunderwriting-app.tf — New Terraform configuration (clone + rename)
- platform/infra/services/ — Updated variables.tf, dev.tfvars, prod.tfvars
- docker-compose.domain.yml — Updated with new services (port 7087 for API)
- .github/workflows/ — Updated CI/CD workflows

**Global references**:
- platform/infra/services/main.tf — May need to import/reference new riskratingandunderwriting-app.tf module if using modular Terraform
- RiskInsure.slnx — Already includes riskratingandunderwriting service projects (created during code clone phase)
- services/policy/src/Endpoint.In/Handlers/ — Verify QuoteAccepted handler routes to new endpoint if applicable
- services/policylifecyclemgt/src/Endpoint.In/Handlers/ — Verify QuoteAccepted handler routes to new endpoint if applicable

**Verification**

1. Local verification: docker compose -f docker-compose.domain.yml up riskratingandunderwriting-api riskratingandunderwriting-endpoint → test API at http://localhost:7087/api/riskratingunderwriting/health
2. CI build: Push commit with updated workflows → verify riskratingandunderwriting images appear in ACR with correct tag
3. Terraform deployment: cd platform/infra/services && terraform plan -var-file=dev.tfvars → review plan includes riskratingandunderwriting resources, apply → verify Container Apps in Azure Portal
4. Health checks: curl https://{riskratingandunderwriting-api-fqdn}/api/riskratingunderwriting/health returns HTTP 200
5. NServiceBus: Send test RateQuote command → verify processed by rau-endpoint, check Service Bus topology (queues, topics, subscriptions)
6. Data isolation: Query both "ratingunderwriting" and "riskratingandunderwriting" Cosmos containers → confirm separate data, no cross-reads
7. Event publishing: Verify rating/underwriting decision events appear in Service Bus topics, subscribed by dependency services
8. Cross-service integration: Send quote through Policy service → verify reaches rau-endpoint → check downstream billing impacts
9. Cosmos RU/s: Monitor RU consumption with both services running in parallel → verify provisioned capacity sufficient to avoid throttling
10. Application Insights: Compare error rates, latency, dependency telemetry between ratingandunderwriting-app and rau-app during parallel run
11. Cutover readiness: Verify traffic routing configuration in API Gateway/Front Door points to new rau-api FQDN
12. Parallel run validation: Run identical quote workflows through both services → confirm results match (ratings, decisions, premium calculations)

**Decisions**

- **Cosmos container**: Use separate "riskratingandunderwriting" container (no shared data with RatingAndUnderwriting) for clean domain separation
- **NServiceBus endpoint**: New endpoint name RiskInsure.RiskRatingAndUnderwriting.Endpoint (no routing conflicts, distinct from RatingAndUnderwriting)
- **API routes**: Update from /api/ratingandunderwriting to /api/riskratingunderwriting as per domain rename (if applicable, verify actual routes)
- **Deployment strategy**: Parallel run with gradual traffic shift via infrastructure routing (not feature flags in code)
- **Replica configuration**: Start with same capacity as RatingAndUnderwriting (API: 1-10, Endpoint: 1-5) to handle equivalent load
- **Rollback approach**: Infrastructure-level traffic routing reversible without code changes; ratingandunderwriting-app remains deployed until rau proven stable
- **Service naming consistency**: "riskratingandunderwriting" lowercase for Terraform/Docker, RiskRatingAndUnderwriting in C# namespaces (follows existing pattern)

**Dependencies and Integration Points**

- **Shared infrastructure**: Uses same Key Vault secrets (Cosmos, Service Bus connection strings), managed identity, Application Insights
- **Service Bus topology**: New subscriptions created automatically for RiskInsure.RiskRatingAndUnderwriting.Endpoint; may need to migrate QuoteAccepted subscriptions from old to new gradually
- **Cross-service impacts**: Verify downstream services (Policy, PolicyLifeCycleMgt, possibly others) correctly route to both old and new endpoints during transition
- **Message routing tables**: Check routing configurations in Policy.Program.cs, PolicyLifeCycleMgt.Program.cs, and any other subscribers to ensure gradual message migration
- **API Gateway/Front Door**: If using centralized routing, update upstream routing rules to include rau-api FQDN and implement canary/traffic split strategy

**Risk Mitigations**

- **Config drift**: Both ratingunderwriting-app.tf and riskratingandunderwriting-app.tf share identical structure except naming → use diff tools to verify alignment before apply
- **Cosmos provisioning**: Ensure sufficient RU/s provisioned for parallel containers (RatingUnderwriting + RiskRatingAndUnderwriting) to avoid throttling; monitor RU consumption during parallel run
- **NServiceBus installers**: First deployment with EnableInstallers() may need elevated Service Bus permissions → verify Managed Identity has "Azure Service Bus Data Owner" role
- **Image tag sync**: CI/CD must build both ratingandunderwriting and riskratingandunderwriting from same commits to maintain version parity during parallel run
- **Message duplication**: During gradual routing, ensure QuoteAccepted doesn't route to both endpoints simultaneously → implement routing table migration strategy
- **Underwriting logic**: Validate that rating/underwriting decision algorithms produce identical results between old and new service (test with sample quotes)
- **Database state consistency**: If QuoteAccepted routing changes mid-session, verify quotes don't get processed twice or missed → idempotency key validation critical
