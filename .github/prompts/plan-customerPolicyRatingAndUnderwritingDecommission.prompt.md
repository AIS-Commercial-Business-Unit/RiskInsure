# Plan: Decommission Legacy Customer, Policy, and RatingAndUnderwriting Services

**TL;DR**: Safely retire the legacy Customer, Policy, and RatingAndUnderwriting services by: (1) moving all code to `services/legacy/` for safe archival, (2) removing shared runtime artifacts in Terraform for Service Bus, Cosmos DB, and outputs, (3) destroying Azure Container Apps through dedicated decommission workflows so Terraform remote state remains correct, (4) optionally deleting the corresponding ACR repositories after successful destroy, and (5) cleaning up references from active workflows, scripts, docker compose, managed services registry, and solution file. Successor services remain active. All changes are intended to be low-risk, operationally controlled, and state-safe.

---

## Steps

### Phase 0: Code Archival & Safety (Parallel with Phase 1)

1. **Create legacy directory structure**
   - Create `services/legacy/` folder if it does not already exist
   - Move entire service folders:
     - `services/customer/` -> `services/legacy/customer/`
     - `services/policy/` -> `services/legacy/policy/`
     - `services/ratingandunderwriting/` -> `services/legacy/ratingandunderwriting/`
   - Preserve all directory structure, tests, docs, and docker-related files

2. **Verify solution file buildability**
   - Update `RiskInsure.slnx` to remove 5 projects for each retired service:
     - Customer:
       - `services/customer/src/Api/Api.csproj`
       - `services/customer/src/Domain/Domain.csproj`
       - `services/customer/src/Infrastructure/Infrastructure.csproj`
       - `services/customer/src/Endpoint.In/Endpoint.In.csproj`
       - `services/customer/test/Unit.Tests/Unit.Tests.csproj`
     - Policy:
       - `services/policy/src/Api/Api.csproj`
       - `services/policy/src/Domain/Domain.csproj`
       - `services/policy/src/Infrastructure/Infrastructure.csproj`
       - `services/policy/src/Endpoint.In/Endpoint.In.csproj`
       - `services/policy/test/Unit.Tests/Unit.Tests.csproj`
     - RatingAndUnderwriting:
       - `services/ratingandunderwriting/src/Api/Api.csproj`
       - `services/ratingandunderwriting/src/Domain/Domain.csproj`
       - `services/ratingandunderwriting/src/Infrastructure/Infrastructure.csproj`
       - `services/ratingandunderwriting/src/Endpoint.In/Endpoint.In.csproj`
       - `services/ratingandunderwriting/test/Unit.Tests/Unit.Tests.csproj`
   - Run `dotnet restore && dotnet build` to confirm zero breakage
   - Suggested commit: `Move Customer, Policy, and RatingAndUnderwriting to legacy and update solution`

### Phase 1: Terraform Shared Infrastructure Decommission

3. **Comment Service Bus artifacts (servicebus.tf)**
   - File: `platform/infra/shared-services/servicebus.tf`
   - Comment Customer queue and topics:
     - Queue: `RiskInsure.Customer.Endpoint`
     - Topics:
       - `RiskInsure.Customer.Domain.Contracts.Events.CustomerClosed`
       - `RiskInsure.Customer.Domain.Contracts.Events.CustomerCreated`
       - `RiskInsure.Customer.Domain.Contracts.Events.CustomerInformationUpdated`
   - Comment Policy queue, topics, and subscription:
     - Queue: `RiskInsure.Policy.Endpoint`
     - Topics:
       - `RiskInsure.Policy.Domain.Contracts.Events.PolicyBound`
       - `RiskInsure.Policy.Domain.Contracts.Events.PolicyCancelled`
       - `RiskInsure.Policy.Domain.Contracts.Events.PolicyIssued`
       - `RiskInsure.Policy.Domain.Contracts.Events.PolicyReinstated`
     - Subscription:
       - `quote_accepted_to_policy`
   - Comment RatingAndUnderwriting queue and topics:
     - Queue: `RiskInsure.RatingAndUnderwriting.Endpoint`
     - Topics:
       - `RiskInsure.RatingAndUnderwriting.Domain.Contracts.Events.QuoteCalculated`
       - `RiskInsure.RatingAndUnderwriting.Domain.Contracts.Events.QuoteDeclined`
       - `RiskInsure.RatingAndUnderwriting.Domain.Contracts.Events.QuoteStarted`
       - `RiskInsure.RatingAndUnderwriting.Domain.Contracts.Events.UnderwritingSubmitted`
   - Add header comments for each service section
   - Suggested commit: `Decommission: Comment Customer, Policy, and RatingAndUnderwriting Service Bus queue/topics/subscriptions`

4. **Comment Cosmos Containers (cosmosdb.tf)**
   - File: `platform/infra/shared-services/cosmosdb.tf`
   - Comment Customer containers:
     - `azurerm_cosmosdb_sql_container.customer`
     - `azurerm_cosmosdb_sql_container.customer_sagas`
   - Comment Policy containers:
     - `azurerm_cosmosdb_sql_container.policy`
     - `azurerm_cosmosdb_sql_container.policy_sagas`
   - Comment RatingAndUnderwriting containers:
     - `azurerm_cosmosdb_sql_container.ratingunderwriting`
     - `azurerm_cosmosdb_sql_container.ratingunderwriting_sagas`
   - Add header comments for each service section
   - Suggested commit: `Decommission: Comment Customer, Policy, and RatingAndUnderwriting Cosmos containers`

5. **Remove Terraform outputs**
   - File: `platform/infra/services/outputs.tf`
   - Comment or remove:
     - `output "customer_api_url"`
     - `output "policy_api_url"`
     - `output "ratingandunderwriting_api_url"`
   - Suggested commit: `Decommission: Comment Customer, Policy, and RatingAndUnderwriting outputs`

### Phase 2: Azure Runtime Cleanup Through Controlled Decommission Workflows

6. **Create dedicated decommission workflows for Azure Container Apps and ACR repos**
   - Use `.github/workflows/decommission-legacy-billing-dev.yml` as the template
   - Create one workflow per service:
     - `decommission-legacy-customer-dev.yml`
     - `decommission-legacy-policy-dev.yml`
     - `decommission-legacy-ratingandunderwriting-dev.yml`
   - Each workflow should:
     - Authenticate to Azure
     - Initialize Terraform in `platform/infra/services`
     - Back up the remote state snapshot before destroy
     - Verify the target service container app resources are present in state
     - Run targeted `terraform plan -destroy` for only the two service container apps
     - Optionally run `terraform apply`
     - Verify Azure container apps are removed after apply
     - Verify Terraform state bindings are removed after apply
     - Optionally delete matching ACR repositories
   - Preserve the same concurrency protection used by Billing to avoid remote state locking conflicts
   - Suggested commit: `Decommission: Add targeted destroy workflows for Customer, Policy, and RatingAndUnderwriting container apps`

7. **Do not rely on CI/CD deployment workflows to destroy container apps**
   - Standard CI/CD workflows should not be used as the destroy mechanism
   - Commenting Terraform alone does not remove Azure Container Apps or ACR repositories
   - Targeted decommission workflows are required to keep the Terraform state in Azure Storage consistent while performing destructive actions

### Phase 3: CI/CD & Operational Cleanup

8. **Update GitHub Actions workflows**
   - File: `.github/workflows/cd-services-dev.yml`
     - Remove `customer)` case block in the service deployment switch
     - Remove `policy)` case block in the service deployment switch
     - Remove `ratingandunderwriting)` case block in the service deployment switch
   - File: `.github/workflows/pr-unit-tests.yml`
     - Remove `customer` from service matrix
     - Remove `policy` from service matrix
     - Remove `ratingandunderwriting` from service matrix
   - File: `.github/workflows/ci-test-integration.yml`
     - Remove `customer`, `policy`, and `ratingandunderwriting` from `SERVICES` array and service mapping
     - Remove related queue checks
     - Remove related Cosmos container checks
   - File: `.github/scripts/managed-services.sh`
     - Remove `customer`, `policy`, and `ratingandunderwriting` from the active managed services list
   - Suggested commit: `Decommission: Remove Customer, Policy, and RatingAndUnderwriting from active CI/CD workflows`

9. **Update Docker Compose**
   - File: `docker-compose.domain.yml`
   - Comment or remove:
     - `customer-api`
     - `customer-endpoint`
     - `policy-api`
     - `policy-endpoint`
     - `ratingandunderwriting-api`
     - `ratingandunderwriting-endpoint`
   - Keep successor services active
   - Suggested commit: `Decommission: Remove Customer, Policy, and RatingAndUnderwriting services from docker-compose.domain.yml`

10. **Update/Comment PowerShell and diagnostics scripts**
   - File: `scripts/docker-start.ps1`
     - Remove Customer, Policy, and RatingAndUnderwriting port output
   - File: `scripts/docker-status.ps1`
     - Remove Customer, Policy, and RatingAndUnderwriting health checks
   - File: `scripts/docker-logs.ps1`
     - Remove or update examples for these retired services
   - File: `scripts/smoke-test.ps1`
     - Remove retired services from endpoint map, expected containers, and smoke tests
   - File: `test/e2e/run-with-diagnostics.ps1`
     - Remove Customer, Policy, and RatingAndUnderwriting port checks and container references
   - Suggested commit: `Decommission: Update scripts to remove Customer, Policy, and RatingAndUnderwriting references`

### Verification

11. **Terraform validation**
   - Run:
     ```bash
     cd platform/infra/foundation && terraform validate
     cd platform/infra/shared-services && terraform validate
     cd platform/infra/services && terraform validate
     ```
   - All should show: `Success! The configuration is valid.`

12. **Decommission workflow dry-run**
   - Run each new decommission workflow in `plan` mode first
   - Confirm only the intended container app resources are targeted
   - Confirm a remote state snapshot artifact is produced

13. **Decommission workflow apply**
   - Run each new decommission workflow in `apply` mode one service at a time
   - Confirm:
     - Azure container apps are removed
     - Terraform state bindings are removed
     - Matching ACR repositories are deleted if requested

14. **Build & Test Validation**
   - Run `dotnet build` from root after removing the archived services from `RiskInsure.slnx`
   - Run `dotnet test` for remaining active services
   - Verify successor services remain unaffected:
     - `customerrelationshipsmgt`
     - `policyequityandinvoicingmgt`
     - `policylifecyclemgt`
     - `riskratingandunderwriting`

15. **Docker Compose Test**
   - Run local compose using the domain compose file
   - Verify retired services are no longer started or expected
   - Verify successor services still start normally

16. **Terraform Plan Dry-Run**
   - Run `terraform plan` in the cloud environment if available
   - Verify no unintended recreation of retired service container apps
   - Verify no syntax errors and only expected resource changes

---

## Relevant Files

### Terraform Files to Modify
- `platform/infra/shared-services/servicebus.tf` — Comment queue, topics, and Policy subscription
- `platform/infra/shared-services/cosmosdb.tf` — Comment Customer, Policy, and RatingAndUnderwriting containers and saga containers
- `platform/infra/services/outputs.tf` — Remove retired service API outputs

### Workflow Files to Add or Modify
- `.github/workflows/decommission-legacy-billing-dev.yml` — Template for new decommission workflows
- `.github/workflows/decommission-legacy-customer-dev.yml` — New targeted destroy workflow
- `.github/workflows/decommission-legacy-policy-dev.yml` — New targeted destroy workflow
- `.github/workflows/decommission-legacy-ratingandunderwriting-dev.yml` — New targeted destroy workflow
- `.github/workflows/cd-services-dev.yml` — Remove deployment targeting
- `.github/workflows/pr-unit-tests.yml` — Remove services from matrix
- `.github/workflows/ci-test-integration.yml` — Remove service references
- `.github/scripts/managed-services.sh` — Remove services from managed list

### Solution & Project Files to Update
- `RiskInsure.slnx` — Remove 15 total projects across the 3 retired services
- `services/customer/` -> `services/legacy/customer/`
- `services/policy/` -> `services/legacy/policy/`
- `services/ratingandunderwriting/` -> `services/legacy/ratingandunderwriting/`

### Docker Compose Files
- `docker-compose.domain.yml` — Remove retired service definitions

### Script Files
- `scripts/docker-start.ps1`
- `scripts/docker-status.ps1`
- `scripts/docker-logs.ps1`
- `scripts/smoke-test.ps1`
- `test/e2e/run-with-diagnostics.ps1`

---

## Verification Checklist

1. `services/legacy/customer/`, `services/legacy/policy/`, and `services/legacy/ratingandunderwriting/` exist with all original files
2. `dotnet build` succeeds with no Customer, Policy, or RatingAndUnderwriting projects in `RiskInsure.slnx`
3. Terraform validate passes in all 3 layers
4. New decommission workflows produce valid destroy plans
5. Azure container apps are destroyed through targeted workflow runs without corrupting Terraform state
6. Matching ACR repositories are removed when requested
7. Active workflows no longer reference Customer, Policy, or RatingAndUnderwriting
8. Local compose and scripts no longer expect these retired services
9. Successor services remain operational

---

## Decisions Made

- **Code Archival**: Move service folders to `services/legacy/` instead of deleting them
- **Shared Terraform Cleanup**: Comment Service Bus and Cosmos artifacts for preserved context and reversibility
- **Container App Removal**: Use targeted Terraform destroy workflows, not standard deployment workflows
- **ACR Cleanup**: Delete repositories in the decommission workflows after successful apply
- **State Safety**: Preserve Terraform state integrity by using remote-state-aware destroy workflows with backup snapshots
- **Workflow and Script Cleanup**: Remove active service references from CI/CD, compose, and scripts
- **Excluded Scope**: Do not comment container app Terraform blocks, do not remove variables, do not update documentation, do not change `.github/workflows/legacy/service-tests.yml`, `.github/workflows/ops-rollback.yml`, or `.github/workflows/new/terraform-plan-simplified.yml`, and do not include Terraform fmt work

---

## Further Considerations

1. **State Safety First**: Continue using dedicated decommission workflows for destructive Azure actions because comment-only Terraform changes do not remove Azure resources
2. **Future Re-Creation Risk**: If the container app resources remain active in Terraform, a future full apply may recreate them; verify intended long-term behavior before finalizing the end state
3. **Data Backup**: Export Customer, Policy, and RatingAndUnderwriting Cosmos data before deletion if required for audit or compliance
4. **Monitoring & Alerts**: Verify no dashboards, alerts, or downstream automation still reference the retired container apps or endpoints
5. **Successor Validation**: Confirm `customerrelationshipsmgt`, `policyequityandinvoicingmgt`, `policylifecyclemgt`, and `riskratingandunderwriting` fully cover required production responsibilities before final apply

---

## Assessment: Is This Approach Good?

Yes, with one important constraint.

✅ **State-safe**: Azure container apps are destroyed through Terraform so the remote state in Azure Storage remains correct

✅ **Operationally proven**: Billing already validated this workflow pattern

✅ **Non-destructive for code**: Service code is archived, not deleted

✅ **Reversible at source level**: Archived services remain recoverable in Git and under `services/legacy/`

✅ **Low-risk rollout**: One service can be decommissioned at a time using plan/apply workflow control

✅ **Audit-friendly**: Shared Terraform comments preserve decommission context

✅ **Clean operational state**: Active workflows, scripts, and compose files stop treating the services as live

⚠️ **Important caveat**: If active Terraform resource blocks for the container apps remain unchanged, future full applies may recreate the apps. That risk must be explicitly accepted or addressed in a follow-up refinement.

This is a strong decommission approach for production systems where Terraform state safety matters and destructive Azure operations must be tightly controlled.
