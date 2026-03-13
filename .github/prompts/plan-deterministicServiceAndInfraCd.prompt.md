## Plan: Deterministic Service and Infra CD

Use CI as the single source of truth for service deployment scope and image tag, then make infra CD path-scoped and plan-aware so apply happens only when Terraform detects real changes. This removes CI/CD scope drift, fixes manual CI dispatch behavior, and ensures infra workflow-file changes verify state without forcing unnecessary deployments.

**Steps**
1. Phase 1: Define a stable CI to CD metadata contract in .github/workflows/ci-build-services.yml. Keep existing service detection behavior, but treat deployment-inputs.json as authoritative output for downstream CD (services array, image_tag, matrix_empty, optional trigger_reason). This phase blocks Step 2.
2. Phase 1: Refactor .github/workflows/cd-services-dev.yml determine job to artifact-first behavior for workflow_run events by downloading the cd-inputs artifact from the triggering CI run-id and parsing deployment-inputs.json. Fail fast if artifact is missing or malformed instead of re-deriving from git. This depends on Step 1.
3. Phase 1: Keep workflow_dispatch support in .github/workflows/cd-services-dev.yml for manual CD runs, but validate the services input against the managed deployable set and compute matrix_empty consistently. This can run in parallel with Step 2 after shared helper logic is agreed.
4. Phase 1: Remove duplicate and conflicting workflow_run git diff logic from .github/workflows/cd-services-dev.yml, including the cd-services-dev.yml file-change deploy-all branch. Keep ci-build-services.yml change handling in CI only, so CD inherits deploy-all from artifact metadata. This depends on Step 2.
5. Phase 1: Add an optional pre-deploy image existence check in .github/workflows/cd-services-dev.yml (ACR tag lookup for each selected service image pair) to fail before Terraform if CI/CD scope mismatch ever regresses. This is parallel with Step 4.
6. Phase 2: Narrow infra push triggers in .github/workflows/cd-infra-dev.yml to foundation/shared-services plus workflow file changes only: platform/infra/foundation/**, platform/infra/shared-services/**, and .github/workflows/cd-infra-dev.yml. This blocks Step 7.
7. Phase 2: In foundation and shared-services jobs inside .github/workflows/cd-infra-dev.yml, add a plan-analysis step after Terraform Plan (terraform show -json tfplan + jq) to compute has_changes and changed_resource_count outputs. This depends on Step 6.
8. Phase 2: Gate Terraform Apply in both infra jobs on existing action policy plus has_changes=true, so workflow-file-only runs still verify but only apply if Terraform detects actual drift/new resources. This depends on Step 7.
9. Phase 2: Keep verify job behavior in .github/workflows/cd-infra-dev.yml active for push runs (including workflow-file changes) so current infrastructure is validated even when apply is skipped due no-op plan. This depends on Step 8.
10. Phase 2: Enhance infra summary output in .github/workflows/cd-infra-dev.yml to clearly show per-layer planned change count and apply skipped/applied status for auditability. This is parallel with Step 9 after Step 7.
11. Phase 3: Align service naming boundaries across CI/CD and Terraform targeting logic by explicitly preserving the managed deployable set in .github/workflows/ci-build-services.yml and .github/workflows/cd-services-dev.yml (exclude non-managed folders under services by design). This can run in parallel with Steps 6-10.
12. Phase 3: Run scenario-based validation in GitHub Actions for push and manual workflows (specific service, all services, workflow-file changes, and no-op infra plans), then capture expected outcomes in workflow summaries for future triage. This depends on Steps 1-11.

**Relevant files**
- c:\Code\AIS\CAIS\RiskInsure\.github\workflows\ci-build-services.yml — source-of-truth service detection and cd-inputs artifact production.
- c:\Code\AIS\CAIS\RiskInsure\.github\workflows\cd-services-dev.yml — downstream deployment scope resolution, terraform target building, and deployment orchestration.
- c:\Code\AIS\CAIS\RiskInsure\.github\workflows\cd-infra-dev.yml — infra trigger filters, foundation/shared-services plan/apply gating, verification, and summary.
- c:\Code\AIS\CAIS\RiskInsure\platform\infra\foundation\main.tf — foundation layer resources affected by plan-change gating.
- c:\Code\AIS\CAIS\RiskInsure\platform\infra\shared-services\main.tf — shared-services layer dependencies and outputs used by verification.
- c:\Code\AIS\CAIS\RiskInsure\platform\infra\shared-services\servicebus.tf — key infra surface likely to produce plan deltas in shared-services runs.
- c:\Code\AIS\CAIS\RiskInsure\platform\infra\shared-services\cosmosdb.tf — key infra surface likely to produce plan deltas in shared-services runs.
- c:\Code\AIS\CAIS\RiskInsure\platform\infra\services\*.tf — service container app target names consumed by CD service selective deployment.

**Verification**
1. Push change only to .github/workflows/ci-build-services.yml on main and confirm CI builds all managed services, publishes cd-inputs with full service list, and CD deploys all from artifact data.
2. Push change to one managed service path under services/<name>/ and confirm CI builds only that service and CD deploys only that service.
3. Manually run CI: Build Services with services=<single> on main and confirm CD auto-deploys only that service (no git-diff override).
4. Manually run CI: Build Services with services=all on main and confirm CD auto-deploys all managed services.
5. Push workflow-only change to .github/workflows/cd-infra-dev.yml and confirm infra workflow runs plan + verify, while apply executes only on layers where has_changes=true.
6. Push change under platform/infra/foundation/** and confirm infra foundation plan detects changes and applies only if non-empty.
7. Push change under platform/infra/shared-services/** and confirm infra shared-services plan detects changes and applies only if non-empty.
8. Push change outside the infra trigger scope and confirm cd-infra-dev.yml does not trigger.

**Decisions**
- Service scope: managed deployable set only (exclude nsb.sales, premium, .nsb_example for now).
- Manual CI branch behavior: CD auto-follow remains restricted to main/Dev.
- Infra push scope: foundation + shared-services + cd-infra workflow file only.
- Workflow-file-only infra changes: run verification and apply only when Terraform plan has actual changes.

**Further Considerations**
1. Recommendation: Add a small shared script in .github/scripts to parse and validate managed service lists once, reducing drift risk between CI and CD manual input parsing.
2. Recommendation: Add a negative test path in docs for invalid manual service names to ensure workflow fails fast with clear error output.
3. Recommendation: If infra modules are introduced later and consumed by foundation/shared-services, expand cd-infra path filters to include platform/infra/modules/**.