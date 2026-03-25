# Plan: Remove Active Legacy Service References (Billing, Customer, Policy, RatingAndUnderwriting)

**TL;DR**: Keep all legacy folders untouched, and remove only active references to legacy services from non-legacy code/config/workflow/docs. For documentation, remove only sections that imply those services are currently active.

---

## Guardrails (Must Follow)

1. **Do not touch legacy folders**:
   - `platform/infra/services/legacy-Do-Not-Use/**`
   - `services/legacy/**`

2. **Target legacy service identifiers outside legacy folders**:
   - `billing`
   - `customer` (legacy service context only)
   - `policy` (legacy service context only)
   - `ratingandunderwriting`

3. **Documentation rule**:
   - Remove only sections that imply these services are currently active.
   - Keep clearly historical/decommission context.

---

## Phase 1: Discovery & Safety Inventory

1. Run repo-wide search for legacy identifiers excluding legacy folders:
   - `billing`
   - `customer`
   - `policy`
   - `ratingandunderwriting`
   - `RiskInsure.Billing.Endpoint`
   - `RiskInsure.Customer.Endpoint`
   - `RiskInsure.Policy.Endpoint`
   - `RiskInsure.RatingAndUnderwriting.Endpoint`

2. Build a categorized inventory of hits:
   - Workflows (`.github/workflows/**`)
   - Workflow scripts (`.github/scripts/**`)
   - Terraform active infra (`platform/infra/**`, excluding `legacy-Do-Not-Use`)
   - Solution/build wiring (`RiskInsure.slnx`, project refs)
   - Compose/scripts (`docker-compose*.yml`, `scripts/**`, `test/e2e/**`)
   - Docs (`README.md`, `docs/**`, `TERRAFORM-ANALYSIS.md`, roadmap files)

3. Mark each hit as one of:
   - Remove (active reference)
   - Keep (historical/legacy-only context)
   - Needs manual domain check (ambiguous `customer`/`policy` string)

---

## Phase 2: CI/CD and Automation Cleanup

1. **Workflows**: remove legacy service entries from active job logic:
   - Matrices
   - service switch/case blocks
   - input options/lists
   - path filters and conditional checks

2. **Managed services registry**:
   - Ensure `.github/scripts/managed-services.sh` contains only active services.

3. **Integration/unit test workflows**:
   - Remove legacy services from any `SERVICES` arrays, maps, and health checks.

4. **Rollback/plan helper workflows**:
   - Remove legacy service options from operational workflows still in use.

---

## Phase 3: Active Terraform Cleanup (Outside Legacy Folder)

1. In `platform/infra/shared-services/servicebus.tf`:
   - Remove active references to legacy service queues, topics, and subscriptions.

2. In `platform/infra/shared-services/cosmosdb.tf`:
   - Remove active legacy Cosmos container/saga container blocks and references.

3. In `platform/infra/services/outputs.tf`:
   - Remove outputs that expose legacy service endpoints.

4. In `platform/infra/services/variables.tf`:
   - Remove legacy service config keys if still present in active maps/defaults.

5. Keep all files under `platform/infra/services/legacy-Do-Not-Use/**` untouched.

---

## Phase 4: Solution, Compose, and Script Cleanup

1. **Solution/build**:
   - Remove any remaining non-legacy build references to legacy services.

2. **Docker compose files**:
   - Remove active service definitions and dependencies for legacy services in non-legacy compose files.

3. **Ops scripts**:
   - Remove status checks, log references, port banners, and smoke tests for legacy services.

4. **E2E diagnostics scripts**:
   - Remove container/port/reference checks for legacy services.

---

## Phase 5: Documentation Cleanup (Active-Only Claims)

1. Update docs to remove only sections that imply legacy services are currently active:
   - Active service lists
   - Current deployment/service topology tables
   - Current local run instructions that include legacy services

2. Keep historical/decommission notes where clearly marked as past state.

3. If needed, reword ambiguous sections from present tense to historical context instead of deleting.

---

## Validation Checklist

1. **Reference scope check**:
   - Legacy identifiers should only remain in:
     - `services/legacy/**`
     - `platform/infra/services/legacy-Do-Not-Use/**`
     - explicitly historical doc sections (if retained)

2. **Workflow sanity**:
   - Active workflows parse and no longer include legacy services in matrices/switches.

3. **Terraform sanity**:
   - `terraform fmt` and `terraform validate` pass for active infra layers.

4. **Build/test sanity**:
   - `dotnet build` succeeds.
   - Relevant CI tests/workflow configs remain valid.

5. **Runtime sanity**:
   - Compose and smoke scripts no longer expect legacy services.

---

## Suggested Execution Sequence

1. Discovery inventory and classification.
2. CI/CD and managed-services cleanup.
3. Terraform shared-services/outputs/variables cleanup.
4. Compose/scripts cleanup.
5. Documentation active-only cleanup.
6. Full validation pass.

---

## Non-Goals

1. Do not modify or reformat files under legacy folders.
2. Do not delete archived code under `services/legacy/**`.
3. Do not remove historical context that is clearly labeled as legacy/decommissioned.
