## Plan: Deploy PolicyEquityAndInvoicingMgt (peimgt-app) with Zero Downtime

Clone Billing's Terraform infrastructure to deploy PolicyEquityAndInvoicingMgt (peimgt-app) as a parallel service. The service code is complete; this plan focuses on infrastructure provisioning, CI/CD integration, and safe traffic cutover while maintaining Billing until retirement.

**TL;DR**: Create Terraform configuration for peimgt-app Container Apps (API + Endpoint), add to CI/CD pipelines, deploy alongside billing-app, verify functionality with parallel traffic, then gradually cut over. Both services will coexist during the transition using separate Cosmos containers (Billing vs PolicyEquityAndInvoicingMgt), enabling zero-downtime migration. Decision made: use fresh Cosmos container "PolicyEquityAndInvoicingMgt" per MIGRATION-SUMMARY.md.

**Steps**

1. **Create Terraform configuration for peimgt Container Apps**
   - Clone billing-app.tf to peimgt-app.tf
   - Update resource names: azurerm_container_app.peimgt_api and azurerm_container_app.peimgt_endpoint
   - Change container app names to peimgt-api and peimgt-endpoint
   - Update Docker image references to use peimgt-api:${var.image_tag} and peimgt-endpoint:${var.image_tag}
   - Replace environment variable CosmosDb__BillingContainerName with CosmosDb__PolicyEquityAndInvoicingMgtContainerName set to "PolicyEquityAndInvoicingMgt"
   - Keep identical structure: managed identity, Key Vault secrets, ingress on port 8080, Application Insights integration

2. **Add peimgt service configuration to Terraform variables**
   - Update variables.tf services map to add "policyequityandinvoicingmgt" entry
   - Configure with same baseline as billing: cpu = 0.25, memory = "0.5Gi", min_replicas = 1, max_replicas = 5/3, container_name = "PolicyEquityAndInvoicingMgt"
   - Ensure both api and endpoint components defined with enabled = true

3. **Update environment-specific variable files**
   - Add policyequityandinvoicingmgt configuration to dev.tfvars
   - Add matching configuration to prod.tfvars
   - Consider starting with conservative replica counts (min=1, max=3) for initial deployment, can scale up during cutover

4. **Update CI/CD pipelines for Docker image builds**
   - Modify ci-build-services.yml line 63: add "policyequityandinvoicingmgt" to ALL_SERVICES array
   - Update corresponding CD workflow cd-services-dev.yml to include policyequityandinvoicingmgt in deployment targets
   - Verify build matrix will include Dockerfiles at services/policyequityandinvoicingmgt/src/Api/Dockerfile and services/policyequityandinvoicingmgt/src/Endpoint.In/Dockerfile

5. **Add to local Docker Compose for development**
   - Update docker-compose.domain.yml to add peimgt-api and peimgt-endpoint services
   - Configure ports: use 7077:8080 for API (avoiding conflicts with billing:7071, customer:7073, others)
   - Set environment variable CosmosDb__PolicyEquityAndInvoicingMgtContainerName=PolicyEquityAndInvoicingMgt
   - Use same Dockerfile patterns: platform/templates/Dockerfile.api.compose and Dockerfile.endpoint.compose with PROJECT_PATH pointing to peimgt projects
   - Reuse same infrastructure dependencies: cosmos-emulator, rabbitmq, License.xml volume

6. **Initial deployment verification (parallel run phase)**
   - Trigger CI build workflow to build and push peimgt images to ACR
   - Apply Terraform changes: terraform plan, review outputs, then terraform apply in dev environment
   - Verify both peimgt-api and peimgt-endpoint Container Apps start successfully via Azure Portal or CLI
   - Check Application Insights for startup logs and health check responses at /api/policyequityandinvoicing/health
   - Test API endpoints directly via peimgt-api ingress URL (external FQDN)
   - Confirm Cosmos DB container "PolicyEquityAndInvoicingMgt" created and accessible
   - Verify NServiceBus endpoint RiskInsure.PolicyEquityAndInvoicingMgt.Endpoint appears in Service Bus subscriptions

7. **Traffic routing and gradual cutover strategy**
   - **Phase 1 - Shadow traffic**: Route read-only/test traffic to peimgt-api while billing-api handles production load
   - **Phase 2 - Canary**: Use Azure Front Door or API Gateway (if available) to route 10% traffic to peimgt-api, monitor error rates, latency, Cosmos RU consumption
   - **Phase 3 - Progressive rollout**: Increase traffic percentage (25% → 50% → 75% → 100%) over monitoring intervals (e.g., 1-2 days each)
   - **Phase 4 - Full cutover**: Route 100% traffic to peimgt-api, keep billing-app running but idle for quick rollback
   - **Phase 5 - Billing retirement**: After 1-2 weeks of stable peimgt operation, scale billing-app to 0 replicas, then remove from Terraform after data retention period

8. **Monitoring and rollback preparation**
   - Define success metrics: p95 latency <500ms, error rate <1%, Cosmos throttling (429s) <0.1%
   - Set up Azure Monitor alerts for peimgt-api and peimgt-endpoint: high error rates, container restarts, health check failures
   - Document rollback procedure: revert traffic routing percentage, scale up billing-app if needed, Terraform state remains unchanged during traffic shifts
   - Capture baseline metrics from billing-app before cutover begins for comparison

**Verification**

1. Local verification: docker compose -f docker-compose.domain.yml up peimgt-api peimgt-endpoint → test API at http://localhost:7077/api/policyequityandinvoicing/health
2. CI build: Push commit with updated workflows → verify peimgt images appear in ACR
3. Terraform deployment: cd platform/infra/services && terraform plan -var-file=dev.tfvars → review plan, apply → verify Container Apps in Azure Portal
4. Health checks: curl https://{peimgt-api-fqdn}/api/policyequityandinvoicing/health returns HTTP 200
5. NServiceBus: Send test RecordPayment command → verify processed by peimgt-endpoint, check Service Bus dead-letter queues are empty
6. Data isolation: Query Cosmos container "PolicyEquityAndInvoicingMgt" → confirm separate from "Billing" container
7. Cutover success: Compare Application Insights metrics between billing and peimgt during parallel run → latency, throughput, error rates similar

**Decisions**

- **Cosmos container**: Use separate "PolicyEquityAndInvoicingMgt" container (no shared data with Billing) per completed migration
- **NServiceBus endpoint**: New endpoint name RiskInsure.PolicyEquityAndInvoicingMgt.Endpoint (no routing conflicts)
- **API routes**: Prefix changed from /api/billing to /api/policyequityandinvoicing as per domain rename
- **Deployment strategy**: Parallel run with gradual traffic shift (not feature flag in code, but infrastructure-level routing)
- **Replica configuration**: Start with same capacity as billing (API: 1-5, Endpoint: 1-3) to handle equivalent load
- **Rollback approach**: Infrastructure-level traffic routing reversible without code changes; billing-app remains deployed until peimgt proven stable

**Dependencies and Integration Points**

- **Shared infrastructure**: Uses same Key Vault secrets (Cosmos, Service Bus connection strings), managed identity, Application Insights
- **Service Bus topology**: New subscriptions created automatically for RiskInsure.PolicyEquityAndInvoicingMgt.Endpoint when first deployed
- **Cross-service impacts**: Verify no other services route commands/events to RiskInsure.Billing.Endpoint that should go to new endpoint (check routing tables in other services' Program.cs files)
- **API Gateway/Front Door**: If using centralized routing, update upstream routing rules to include peimgt-api FQDN

**Risk Mitigations**

- **Config drift**: Both billing-app.tf and peimgt-app.tf share identical structure except naming → use diff tools to verify alignment before apply
- **Cosmos provisioning**: Ensure sufficient RU/s provisioned for parallel containers (Billing + PolicyEquityAndInvoicingMgt) to avoid throttling
- **NServiceBus installers**: First deployment with EnableInstallers() may need elevated Service Bus permissions → verify Managed Identity has "Azure Service Bus Data Owner" role
- **Image tag sync**: CI/CD must build both billing and peimgt from same commit to maintain version parity during parallel run
